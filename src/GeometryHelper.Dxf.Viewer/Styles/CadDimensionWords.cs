using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The words of a dimension's picture measured as AutoCAD 2021 measures them to lay the picture out: as wide as their
    /// MTEXT's <c>ActualWidth</c> and as high as its <c>ActualHeight</c>, in the font of the dimension's text style, read by the
    /// library (<see cref="DxfDatabase.FontSearchPath"/>) from the font's file. "R20" of arial.ttf 2.5 high is 5.965 wide and
    /// 2.553 high, as AutoCAD's own picture has it, its digits reaching past the top of the A a text's height stands for.
    /// </summary>
    /// <remarks>
    /// A TrueType font is read from the Fonts folders of Windows, by its file (arial.ttf) or, for a style that names a typeface
    /// alone, by the file of that typeface's regular face (<see cref="CadTrueTypeFonts.FileOf"/>). An SHX font, and its big
    /// font, is read from where the fonts last given a document (<see cref="CadDocument.Fonts"/>) find it, the folder of that
    /// document's drawing first. Words of a font not found, or of a style the library does not measure (wider or narrower
    /// than high, slanted), are not measured: the picture then takes a letter as 0.7 of the height wide, as before. The
    /// measurements are kept by font, words and height, at most 4096 at once.
    /// </remarks>
    internal static class CadDimensionWords
    {
        private const int MostKept = 4096;

        private static readonly object Gate = new object();
        private static readonly Dictionary<string, DxfDatabase> Measurers = new Dictionary<string, DxfDatabase>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> StyleNames = new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, KeyValuePair<double, double>> Kept = new Dictionary<string, KeyValuePair<double, double>>(StringComparer.Ordinal);
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
        /// and nothing measured, where the style is null, its font is not found or the library does not measure it.
        /// </summary>
        internal static bool TryMeasure(DxfTextStyleTableRecord style, string words, double height, out double width, out double tall)
        {
            width = 0.0;
            tall = 0.0;

            if (style == null || string.IsNullOrEmpty(words) || !(height > 0.0))
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

            string look = file + "|" + bigFile + "|" + font.TypeFace + "|" + (font.Bold ? "b" : string.Empty) + (font.Italic ? "i" : string.Empty)
                + "|" + xscale.ToString("R", CultureInfo.InvariantCulture) + "|" + oblique.ToString("R", CultureInfo.InvariantCulture);
            string key = look + "|" + height.ToString("R", CultureInfo.InvariantCulture) + "|" + words;

            lock (Gate)
            {
                if (Kept.TryGetValue(key, out KeyValuePair<double, double> known))
                {
                    width = known.Key;
                    tall = known.Value;
                    return width > 0.0;
                }

                Measure(file, bigFile, font, xscale, oblique, look, words, height, out width, out tall);

                if (Kept.Count >= MostKept)
                {
                    Kept.Clear();
                }

                Kept[key] = new KeyValuePair<double, double>(width, tall);
                return width > 0.0;
            }
        }

        private static void Measure(string file, string bigFile, DxfFontDescriptor font, double xscale, double oblique, string look, string words, double height, out double width, out double tall)
        {
            width = 0.0;
            tall = 0.0;
            string extension = Path.GetExtension(file).ToLowerInvariant();
            bool trueType = extension == ".ttf" || extension == ".ttc" || extension == ".otf" || (file.Length == 0 && font.TypeFace.Length > 0);
            var folders = new List<string>();
            string fileName;
            string bigName = string.Empty;

            if (trueType)
            {
                fileName = Path.GetFileName(file.Length > 0 ? file : CadTrueTypeFonts.FileOf(font.TypeFace));
                string path = TrueTypePath(fileName);

                if (path == null)
                {
                    return;
                }

                folders.Add(Path.GetDirectoryName(path));
            }
            else
            {
                CadFonts fonts = _fonts;

                if (fonts == null || file.Length == 0 || CadFonts.IsBuiltIn(file))
                {
                    return;
                }

                string path = fonts.Locate(file, _fontFolder);

                if (path == null)
                {
                    return;
                }

                fileName = Path.GetFileName(path);
                folders.Add(Path.GetDirectoryName(path));

                if (bigFile.Length > 0)
                {
                    string big = fonts.Locate(bigFile, _fontFolder);

                    if (big != null)
                    {
                        bigName = Path.GetFileName(big);
                        folders.Add(Path.GetDirectoryName(big));
                    }
                }
            }

            string search = string.Join(";", folders);

            if (!Measurers.TryGetValue(search, out DxfDatabase database))
            {
                database = new DxfDatabase("AC1032") { FontSearchPath = search };
                Measurers[search] = database;
            }

            // Each look a text style of its own in the measuring drawing, named by the order the looks were first met in.
            if (!StyleNames.TryGetValue(search + "|" + look, out string name))
            {
                name = "W" + StyleNames.Count.ToString(CultureInfo.InvariantCulture);
                StyleNames[search + "|" + look] = name;
                var record = new DxfTextStyleTableRecord { Name = name, FileName = fileName, BigFontFileName = bigName, XScale = xscale, ObliquingAngle = oblique };

                if (font.TypeFace.Length > 0)
                {
                    record.Font = font;
                }

                database.TextStyleTable.Add(record);
            }

            var text = new DxfMText { Location = new GeoPoint3(0.0, 0.0, 0.0), TextHeight = height, Attachment = DxfAttachmentPoint.MiddleCenter, Contents = words, TextStyleName = name };
            database.ModelSpace.AppendEntity(text);

            try
            {
                double measured = text.ActualWidth;
                double high = text.ActualHeight;

                if (measured > 0.0 && high > 0.0 && !double.IsInfinity(measured) && !double.IsInfinity(high))
                {
                    width = measured;
                    tall = high;
                }
            }
            catch (Exception exception) when (exception is InvalidOperationException || exception is ArgumentException)
            {
                GeometryHelperLog.Debug("CadDimensionWords: the words " + words + " of " + fileName + " were not measured (" + exception.Message + ").");
            }
            finally
            {
                text.Erase();
            }
        }

        /// <summary>A TrueType file in the Fonts folder of Windows or of the user's; null where neither has it.</summary>
        private static string TrueTypePath(string fileName)
        {
            if (fileName.Length == 0)
            {
                return null;
            }

            foreach (string folder in new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.Fonts),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Fonts"),
            })
            {
                try
                {
                    string path = string.IsNullOrEmpty(folder) ? null : Path.Combine(folder, fileName);

                    if (path != null && File.Exists(path))
                    {
                        return path;
                    }
                }
                catch (ArgumentException)
                {
                    return null;
                }
            }

            return null;
        }
    }
}
