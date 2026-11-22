using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// Walks a support folder (Fonts, Hatches, Linetypes, Lisps, DrawingTemplates, Plugins) and its subfolders in one order, so
    /// that every kind reads them the same way: depth first, a folder's own files first by name (letters of either case the
    /// same, then as written), then its subfolders in the same name order, each walked the same way. A file at the top level
    /// always comes before one in a subfolder, and where two files have one name the first wins. A subfolder that is a reparse
    /// point (a junction or a symbolic link, so that a loop cannot happen), hidden or a system folder is not entered, and
    /// one that cannot be read is passed over with a Debug line, the walk going on.
    /// </summary>
    internal static class CadSupportWalk
    {
        /// <summary>The folder and every subfolder of it that is walked, in the order of the walk: a folder before the folders in it.</summary>
        /// <param name="root">The folder; one that cannot be read is still the first and only entry.</param>
        /// <returns>The full paths of the folders, the root first.</returns>
        internal static IReadOnlyList<string> Folders(string root)
        {
            var folders = new List<string> { root };
            Enter(root, folders);
            return folders;
        }

        /// <summary>
        /// The subfolders of a folder that are walked, in name order: not a reparse point, not hidden, not a system folder; the
        /// folders of a plugin of the Plugins folder are chosen with this as well.
        /// </summary>
        /// <param name="folder">The folder.</param>
        /// <returns>The full paths of the subfolders; none where the folder cannot be read.</returns>
        internal static IReadOnlyList<string> SubfoldersOf(string folder)
        {
            DirectoryInfo[] found;

            try
            {
                found = new DirectoryInfo(folder).GetDirectories();
            }
            catch (Exception e) when (IsNoAccess(e))
            {
                GeometryHelperLog.Debug("CadSupportWalk: " + folder + " was not listed (" + e.Message + ").");
                return Array.Empty<string>();
            }

            var walked = new List<DirectoryInfo>();

            foreach (DirectoryInfo directory in found)
            {
                try
                {
                    const FileAttributes skipped =
                        FileAttributes.ReparsePoint | FileAttributes.Hidden | FileAttributes.System;

                    if ((directory.Attributes & skipped) == 0)
                    {
                        walked.Add(directory);
                    }
                }
                catch (Exception e) when (IsNoAccess(e))
                {
                    GeometryHelperLog.Debug(
                        "CadSupportWalk: " + directory.FullName + " was passed over (" + e.Message + ")."
                    );
                }
            }

            return walked
                .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(d => d.Name, StringComparer.Ordinal)
                .Select(d => d.FullName)
                .ToList();
        }

        /// <summary>
        /// The files of a folder and its subfolders that a test accepts, in the order of the walk.
        /// </summary>
        /// <param name="root">The folder.</param>
        /// <param name="wanted">Tells from a file name whether the file is listed.</param>
        /// <returns>The full paths.</returns>
        internal static IReadOnlyList<string> Files(string root, Func<string, bool> wanted)
        {
            var files = new List<string>();

            foreach (string folder in Folders(root))
            {
                files.AddRange(FilesOf(folder, wanted));
            }

            return files;
        }

        /// <summary>The files of one folder that a test accepts, by name; none, said at Debug, where it cannot be read.</summary>
        /// <param name="folder">The folder.</param>
        /// <param name="wanted">Tells from a file name whether the file is listed.</param>
        /// <returns>The full paths.</returns>
        internal static IReadOnlyList<string> FilesOf(string folder, Func<string, bool> wanted)
        {
            try
            {
                return Directory
                    .GetFiles(folder)
                    .Where(p => wanted(Path.GetFileName(p)))
                    .OrderBy(p => Path.GetFileName(p), StringComparer.OrdinalIgnoreCase)
                    .ThenBy(p => Path.GetFileName(p), StringComparer.Ordinal)
                    .ToList();
            }
            catch (Exception e) when (IsNoAccess(e) || e is ArgumentException)
            {
                GeometryHelperLog.Debug("CadSupportWalk: " + folder + " was not listed (" + e.Message + ").");
                return Array.Empty<string>();
            }
        }

        /// <summary>
        /// Keeps the first file of each name, in the order given, and says the others at Debug in one line: a font, a pattern, a
        /// linetype or a template of one name in two folders is the first folder's.
        /// </summary>
        /// <param name="what">What the files are, as the line names them: "pattern files".</param>
        /// <param name="paths">The full paths, in the order of the walk.</param>
        /// <returns>The paths kept, in the same order.</returns>
        internal static IReadOnlyList<string> FirstOfEachName(string what, IEnumerable<string> paths)
        {
            var kept = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var result = new List<string>();
            var passed = new List<string>();

            foreach (string path in paths)
            {
                string name = Path.GetFileName(path);

                if (kept.TryGetValue(name, out string first))
                {
                    passed.Add(path + " (" + first + " came first)");
                }
                else
                {
                    kept.Add(name, path);
                    result.Add(path);
                }
            }

            if (passed.Count > 0)
            {
                GeometryHelperLog.Debug(
                    "CadSupportWalk: "
                        + passed.Count
                        + " of the "
                        + what
                        + " passed over by name: "
                        + string.Join("; ", passed)
                        + "."
                );
            }

            return result;
        }

        private static void Enter(string folder, List<string> folders)
        {
            foreach (string subfolder in SubfoldersOf(folder))
            {
                folders.Add(subfolder);
                Enter(subfolder, folders);
            }
        }

        private static bool IsNoAccess(Exception e) =>
            e is IOException || e is UnauthorizedAccessException || e is System.Security.SecurityException;
    }
}
