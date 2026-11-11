using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The user's own support files a drawing's linetypes and hatch patterns may be read from: the folders the user
    /// gave in <see cref="CadDocument.SupportFolders"/>, the drawing's own folder first where there is one. None is looked for
    /// by default (<see cref="DefaultFolders"/> is empty), and no file of AutoCAD's is looked for or preferred: the presenter's
    /// own sets (<see cref="CadLinetypeFile.BuiltIn"/>, <see cref="CadPatternFile.BuiltIn"/>) are the default.
    /// </summary>
    public static class CadSupportFiles
    {
        /// <summary>
        /// Gets the support folders a drawing starts with: none. The user adds a folder of their own in Global settings' Support
        /// file search folders; AutoCAD's Support folders are not looked for.
        /// </summary>
        public static IReadOnlyList<string> DefaultFolders { get; } = Array.Empty<string>();

        /// <summary>
        /// Finds a file of support: the path as written where it is a full path that is there, else its name in the drawing's
        /// folder and then in the document's support folders, in order; null where none has it.
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

                foreach (string folder in FoldersOf(document))
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
        /// Lists the files of support of an extension in the drawing's folder and the support folders, each name once, the
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

            foreach (string folder in FoldersOf(document))
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
        /// The folders looked in, in order: the drawing's, then the document's support folders, each once; none, not even the
        /// drawing's, where the user has given no support folder.
        /// </summary>
        internal static IReadOnlyList<string> FoldersOf(CadDocument document)
        {
            var folders = new List<string>();

            if (document.SupportFolders.Count == 0)
            {
                return folders;
            }

            string own = document.DrawingFolder;

            if (own != null)
            {
                folders.Add(own);
            }

            foreach (string folder in document.SupportFolders)
            {
                if (!folders.Contains(folder, StringComparer.OrdinalIgnoreCase))
                {
                    folders.Add(folder);
                }
            }

            return folders;
        }
    }

    /// <summary>The folders a document's support files are looked for in.</summary>
    public sealed partial class CadDocument
    {
        private IReadOnlyList<string> _supportFolders = Array.Empty<string>();

        /// <summary>
        /// Gets or sets the user's own folders the drawing's linetype files (.lin) and hatch pattern files (.pat) are looked
        /// for in, in order, after the drawing's own folder, which is looked in only where this gives a folder: none
        /// until set; the window sets Global settings' Support file search folders, empty by default. Blank entries are left
        /// out and a folder given twice is kept once.
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

        /// <summary>The folders of <see cref="CadSupportFiles"/>, parted by semicolons, as <see cref="DxfDatabase.PatternSearchPath"/> takes them.</summary>
        internal string PatternSearchPath => string.Join(";", CadSupportFiles.FoldersOf(this));
    }
}
