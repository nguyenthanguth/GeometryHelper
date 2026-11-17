using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// The support files a drawing's linetypes (.lin) and hatch patterns (.pat) may be read from, looked for in this order:
    /// the folders the user gave in <see cref="CadDocument.SupportFolders"/>, then the Linetypes or Hatches folder beside
    /// GeometryHelper.Cad.dll (<see cref="CadSupportFolders"/>), then the presenter's own sets
    /// (<see cref="CadLinetypeFile.BuiltIn"/>, <see cref="CadPatternFile.BuiltIn"/>). The drawing's own folder is not looked
    /// in for them, and no file of AutoCAD's is looked for or preferred.
    /// </summary>
    public static class CadSupportFiles
    {
        /// <summary>
        /// Gets the support folders a drawing starts with: none. The user adds a folder of their own in Global settings' Support
        /// file search folders; the folders beside the dll are looked in after them.
        /// </summary>
        public static IReadOnlyList<string> DefaultFolders { get; } = Array.Empty<string>();

        /// <summary>
        /// Finds a file of support: the path as written where it is a full path that is there, else its name in the document's
        /// support folders in order and then, for a .lin or .pat file, in the Linetypes or Hatches folder beside the dll; null
        /// where none has it.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="fileName">The file's name or path, as mine.lin.</param>
        /// <returns>The full path, or null.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the document is null.</exception>
        public static string Find(CadDocument document, string fileName)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (string.IsNullOrWhiteSpace(fileName))
            {
                return null;
            }

            string name = fileName.Trim().Trim('"');

            try
            {
                if (Path.IsPathRooted(name))
                {
                    return File.Exists(name) ? Path.GetFullPath(name) : null;
                }

                foreach (string folder in FoldersOf(document, KindOf(name)))
                {
                    string path = Path.Combine(folder, name);

                    if (File.Exists(path))
                    {
                        return Path.GetFullPath(path);
                    }
                }
            }
            catch (Exception e)
                when (e is ArgumentException
                    || e is IOException
                    || e is NotSupportedException
                    || e is UnauthorizedAccessException
                    || e is System.Security.SecurityException
                )
            {
                GeometryHelperLog.Debug("CadSupportFiles: " + name + " was not looked for (" + e.Message + ").");
            }

            return null;
        }

        /// <summary>
        /// Lists the files of support of an extension in the support folders and the folder beside the dll, each name once, the
        /// first folder that has it winning, in the order of their names: hidden.lin, mine.lin, walls.lin.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="extension">The extension, as .lin or .pat.</param>
        /// <returns>The full paths.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the document or the extension is null.</exception>
        public static IReadOnlyList<string> List(CadDocument document, string extension)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (extension == null)
            {
                throw new ArgumentNullException(nameof(extension));
            }

            var found = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (string folder in FoldersOf(document, KindOf("x" + extension)))
            {
                try
                {
                    foreach (
                        string path in Directory
                            .GetFiles(folder, "*" + extension)
                            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                    )
                    {
                        string name = Path.GetFileName(path);

                        if (
                            !found.ContainsKey(name)
                            && string.Equals(Path.GetExtension(path), extension, StringComparison.OrdinalIgnoreCase)
                        )
                        {
                            found.Add(name, path);
                        }
                    }
                }
                catch (Exception e)
                    when (e is ArgumentException
                        || e is IOException
                        || e is UnauthorizedAccessException
                        || e is System.Security.SecurityException
                    )
                {
                    GeometryHelperLog.Debug("CadSupportFiles: " + folder + " was not listed (" + e.Message + ").");
                }
            }

            return found.Values.ToList().AsReadOnly();
        }

        /// <summary>
        /// Gets the linetype file a drawing loads from unless told otherwise: the presenter's own set
        /// (<see cref="CadLinetypeFile.BuiltInName"/>), whatever the drawing's units. A file of the user's is loaded only where
        /// the user names it.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <returns><see cref="CadLinetypeFile.BuiltInName"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the document is null.</exception>
        public static string DefaultLinetypeFile(CadDocument document)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            return CadLinetypeFile.BuiltInName;
        }

        /// <summary>
        /// The folders looked in, in order: the document's support folders, then the default folder of the kind beside the dll
        /// where it exists, each once. Of no kind (a file other than .lin and .pat) the drawing's folder comes first as it
        /// always did, and only where the user has given a support folder.
        /// </summary>
        internal static IReadOnlyList<string> FoldersOf(CadDocument document, CadSupportKind? kind)
        {
            var folders = new List<string>();

            if (kind == null)
            {
                if (document.SupportFolders.Count == 0)
                {
                    return folders;
                }

                string own = document.DrawingFolder;

                if (own != null)
                {
                    folders.Add(own);
                }
            }

            foreach (string folder in document.SupportFolders)
            {
                if (!folders.Contains(folder, StringComparer.OrdinalIgnoreCase))
                {
                    folders.Add(folder);
                }
            }

            if (kind != null)
            {
                foreach (string folder in document.SupportRoots.ExistingOf(kind.Value))
                {
                    if (!folders.Contains(folder, StringComparer.OrdinalIgnoreCase))
                    {
                        folders.Add(folder);
                    }
                }
            }

            return folders;
        }

        /// <summary>The kind of default folder a file name belongs in: Linetypes for .lin, Hatches for .pat; none for any other.</summary>
        private static CadSupportKind? KindOf(string fileName)
        {
            string extension = Path.GetExtension(fileName ?? string.Empty);
            return string.Equals(extension, ".lin", StringComparison.OrdinalIgnoreCase) ? CadSupportKind.Linetypes
                : string.Equals(extension, ".pat", StringComparison.OrdinalIgnoreCase) ? CadSupportKind.Hatches
                : (CadSupportKind?)null;
        }
    }

    /// <summary>The folders a document's support files are looked for in.</summary>
    public sealed partial class CadDocument
    {
        private IReadOnlyList<string> _supportFolders = Array.Empty<string>();

        /// <summary>
        /// Gets or sets the user's own folders the drawing's linetype files (.lin) and hatch pattern files (.pat) are looked
        /// for in, in order, before the Linetypes and Hatches folders beside the dll: none until set; the window sets Global
        /// settings' Support file search folders, empty by default. Blank entries are left out and a folder given twice is
        /// kept once.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the value is null.</exception>
        public IReadOnlyList<string> SupportFolders
        {
            get => _supportFolders;
            set =>
                _supportFolders = (value ?? throw new ArgumentNullException(nameof(value)))
                    .Where(f => !string.IsNullOrWhiteSpace(f))
                    .Select(f => f.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList()
                    .AsReadOnly();
        }

        /// <summary>The folder of the drawing's file; null for a drawing of no file.</summary>
        internal string DrawingFolder =>
            _hasPath && !string.IsNullOrEmpty(FileName) ? Path.GetDirectoryName(Path.GetFullPath(FileName)) : null;

        /// <summary>The default folders a test puts in place of those beside the dll.</summary>
        internal CadSupportFolders SupportRoots { get; set; } = CadSupportFolders.Default;

        /// <summary>The hatch pattern folders of <see cref="CadSupportFiles"/>, parted by semicolons, as <see cref="DxfDatabase.PatternSearchPath"/> takes them.</summary>
        internal string PatternSearchPath => string.Join(";", CadSupportFiles.FoldersOf(this, CadSupportKind.Hatches));
    }
}
