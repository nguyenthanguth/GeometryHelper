using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The folder of plot style tables the Plot and Page Setup dialogs list and a plot reads, as AutoCAD's Plot Styles folder,
    /// and the tables the presenter makes itself: <c>acad.ctb</c>, <c>monochrome.ctb</c>, <c>grayscale.ctb</c> and the three
    /// screening tables, <c>acad.stb</c>, <c>monochrome.stb</c> and <c>grayscale.stb</c>. None is a file of AutoCAD's; they are
    /// made by <see cref="BuiltIn"/> and written to the folder by <see cref="EnsureBuiltIns"/>.
    /// </summary>
    /// <remarks>
    /// A document names its folder in <see cref="CadDocument.PlotStyleFolder"/>; where that is empty no disk is read and the
    /// built-in tables stand in, so that a plot of a drawing opened with no window is the same as before tables were files.
    /// Where a built-in table's file is not in the folder (deleted, or the folder made by hand) the built-in table stands in
    /// for it too; one the user has edited or replaced is read as it is.
    /// </remarks>
    public static class CadPlotStyleFolder
    {
        /// <summary>The name of the folder the presenter makes where Global settings give none: PlotStyles under %AppData%\GeometryHelper\Drawing.</summary>
        public static string DefaultPath =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "GeometryHelper",
                "Drawing",
                "PlotStyles"
            );

        /// <summary>
        /// Gets the folder a setting stands for: the setting, trimmed, where it names one; where it is empty the folder
        /// <c>PlotStyles</c> beside the settings file, which is <see cref="DefaultPath"/> for the user's own settings file.
        /// </summary>
        /// <param name="setting">The folder of Global settings, empty for the default.</param>
        /// <param name="settingsFile">The file the settings are kept in (<see cref="GlobalSettings.DefaultPath"/> for the user's); null for that.</param>
        /// <returns>The folder's path.</returns>
        public static string Resolve(string setting, string settingsFile = null)
        {
            if (!string.IsNullOrWhiteSpace(setting))
            {
                return setting.Trim();
            }

            string beside = string.IsNullOrWhiteSpace(settingsFile)
                ? null
                : Path.GetDirectoryName(Path.GetFullPath(settingsFile));
            return string.IsNullOrEmpty(beside) ? DefaultPath : Path.Combine(beside, "PlotStyles");
        }

        /// <summary>Gets the names of the tables the presenter makes, in the order the lists show them, with the extension: <c>acad.ctb</c> and on.</summary>
        public static IReadOnlyList<string> BuiltInNames { get; } =
            new[]
            {
                "acad.ctb",
                "grayscale.ctb",
                "monochrome.ctb",
                "Screening 25%.ctb",
                "Screening 50%.ctb",
                "Screening 75%.ctb",
                "acad.stb",
                "grayscale.stb",
                "monochrome.stb",
            };

        /// <summary>
        /// Makes one of the tables the presenter makes. <c>acad</c> changes nothing; <c>monochrome</c> plots every colour in
        /// black; <c>grayscale</c> plots every colour as the grey of its brightness (dithering and grayscale on, the object's
        /// colour kept); <c>Screening 25%</c>, 50 and 75 plot the object's colour screened to that per cent.
        /// </summary>
        /// <param name="fileName">The name with its extension, one of <see cref="BuiltInNames"/>, letters of either case the same.</param>
        /// <returns>A new table, its <see cref="CadPlotStyleSheet.FileName"/> the name; null where the name is not one of them.</returns>
        public static CadPlotStyleSheet BuiltIn(string fileName)
        {
            string name = BuiltInNames.FirstOrDefault(n =>
                string.Equals(n, (fileName ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase)
            );

            if (name == null)
            {
                return null;
            }

            bool named = name.EndsWith(CadPlotStyleSheet.NamedExtension, StringComparison.OrdinalIgnoreCase);
            CadPlotStyleSheet sheet = named ? CadPlotStyleSheet.NewNamed() : CadPlotStyleSheet.NewColorDependent();
            string kind = name.Substring(0, name.Length - 4);
            sheet.FileName = name;

            foreach (CadPlotStyle style in sheet.Styles)
            {
                switch (kind)
                {
                    case "monochrome":
                        style.Rgb = 0x000000;
                        break;
                    case "grayscale":
                        style.Grayscale = true;
                        break;
                    case "Screening 25%":
                        style.Screening = 25;
                        break;
                    case "Screening 50%":
                        style.Screening = 50;
                        break;
                    case "Screening 75%":
                        style.Screening = 75;
                        break;
                }
            }

            sheet.Description =
                kind == "acad" ? "Every object plots as it is."
                : kind == "monochrome" ? "Every colour plots in black."
                : kind == "grayscale" ? "Every colour plots as the grey of its brightness."
                : "Every colour plots screened to " + kind.Substring(10, 2) + " per cent.";
            return sheet;
        }

        /// <summary>
        /// Gets the names of the tables a folder holds, to list in the Plot dialog: its .ctb files (<paramref name="named"/>
        /// false) or its .stb files, in ordinal order of their names with letters of either case the same, ties by case. Where
        /// <paramref name="folder"/> is empty, the names of the tables the presenter makes of that kind.
        /// </summary>
        /// <param name="folder">The folder; null or empty for none, the built-in tables standing in.</param>
        /// <param name="named">True for named tables (.stb), false for colour-dependent ones (.ctb).</param>
        /// <returns>The names with their extensions, a new list; empty where the folder is missing or holds none.</returns>
        public static IReadOnlyList<string> Names(string folder, bool named)
        {
            string extension = named ? CadPlotStyleSheet.NamedExtension : CadPlotStyleSheet.ColorDependentExtension;
            var names = new List<string>();

            if (string.IsNullOrWhiteSpace(folder))
            {
                names.AddRange(BuiltInNames.Where(n => n.EndsWith(extension, StringComparison.OrdinalIgnoreCase)));
            }
            else
            {
                try
                {
                    if (Directory.Exists(folder))
                    {
                        names.AddRange(
                            Directory
                                .GetFiles(folder, "*" + extension)
                                .Select(Path.GetFileName)
                                .Where(n =>
                                    string.Equals(Path.GetExtension(n), extension, StringComparison.OrdinalIgnoreCase)
                                )
                        );
                    }
                }
                catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
                {
                    GeometryHelperLog.Warn(
                        "CadPlotStyleFolder: the folder " + folder + " could not be read.",
                        exception
                    );
                }
            }

            names.Sort(
                (a, b) =>
                {
                    int byName = string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
                    return byName != 0 ? byName : string.CompareOrdinal(a, b);
                }
            );
            return names;
        }

        /// <summary>
        /// Reads a table of a folder by its name: the file, letters of either case the same; where the file is not there, or
        /// the folder is empty, the built-in table of that name. A file that does not read is said once in the log, as a
        /// warning, and the built-in table of its name stands in where there is one.
        /// </summary>
        /// <param name="folder">The folder; null or empty for none.</param>
        /// <param name="fileName">The table's file name with its extension, as <c>monochrome.ctb</c>.</param>
        /// <returns>The table; null where there is none of the name, or the name is empty.</returns>
        public static CadPlotStyleSheet Load(string folder, string fileName)
        {
            string name = (fileName ?? string.Empty).Trim();

            if (name.Length == 0 || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(folder))
            {
                try
                {
                    string path = FileOf(folder, name);

                    if (path != null)
                    {
                        return CadPlotStyleSheet.Load(path);
                    }
                }
                catch (Exception exception)
                    when (exception is IOException
                        || exception is UnauthorizedAccessException
                        || exception is InvalidDataException
                    )
                {
                    GeometryHelperLog.Warn(
                        "CadPlotStyleFolder: the plot style table " + name + " in " + folder + " does not read.",
                        exception
                    );
                }
            }

            return BuiltIn(name);
        }

        /// <summary>The path of a file of a folder by its name, letters of either case the same; null where the folder has none.</summary>
        private static string FileOf(string folder, string name)
        {
            string path = Path.Combine(folder, name);

            if (File.Exists(path))
            {
                return path;
            }

            if (!Directory.Exists(folder))
            {
                return null;
            }

            return Directory
                .GetFiles(folder)
                .Where(f => string.Equals(Path.GetFileName(f), name, StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => f, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        /// <summary>
        /// Makes the folder where it is missing and writes the tables the presenter makes that it lacks, never one it has:
        /// a table the user edited is kept. The window calls it as it starts and after Global settings change the folder.
        /// </summary>
        /// <param name="folder">The folder.</param>
        /// <returns>The names of the files written, in the order of <see cref="BuiltInNames"/>; empty where the folder has them all or cannot be written to (said in the log).</returns>
        /// <exception cref="ArgumentException">Thrown when the folder is empty.</exception>
        public static IReadOnlyList<string> EnsureBuiltIns(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder))
            {
                throw new ArgumentException("A folder of plot style tables needs a path.", nameof(folder));
            }

            var written = new List<string>();

            try
            {
                Directory.CreateDirectory(folder);

                foreach (string name in BuiltInNames)
                {
                    if (FileOf(folder, name) == null)
                    {
                        BuiltIn(name).Save(Path.Combine(folder, name));
                        written.Add(name);
                    }
                }
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                GeometryHelperLog.Warn(
                    "CadPlotStyleFolder: the folder " + folder + " could not be written to.",
                    exception
                );
            }

            return written;
        }

        /// <summary>
        /// Says why a name cannot name a plot style table file, as Windows refuses one: empty, longer than 120 letters, or
        /// with a character a file name cannot hold. The extension is not part of the name.
        /// </summary>
        /// <param name="name">The name typed, without .ctb or .stb.</param>
        /// <returns>The reason, in a sentence; null where the name is a good one.</returns>
        public static string CheckFileName(string name)
        {
            string trimmed = (name ?? string.Empty).Trim();

            if (trimmed.Length == 0)
            {
                return "A plot style table needs a name.";
            }

            if (trimmed.Length > 120)
            {
                return "A plot style table's name is 120 letters at most.";
            }

            if (
                trimmed.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                || trimmed.EndsWith(".", StringComparison.Ordinal)
            )
            {
                return "A plot style table's name may not hold any of \\ / : * ? \" < > |, or end in a point.";
            }

            return null;
        }
    }
}
