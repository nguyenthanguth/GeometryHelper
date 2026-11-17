using System;
using System.Collections.Generic;
using System.IO;

namespace GeometryHelper.Cad
{
    /// <summary>The kinds of support file that have a default folder next to GeometryHelper.Cad.dll.</summary>
    public enum CadSupportKind
    {
        /// <summary>Fonts: SHX fonts and big fonts, and TrueType files (.ttf, .ttc, .otf).</summary>
        Fonts,

        /// <summary>Hatch pattern files (.pat).</summary>
        Hatches,

        /// <summary>Linetype files (.lin).</summary>
        Linetypes,
    }

    /// <summary>
    /// The default support folders, as AutoCAD's own Fonts and Support folders are: <c>Fonts</c>, <c>Hatches</c> and
    /// <c>Linetypes</c> beside GeometryHelper.Cad.dll (not beside the exe, so that a host which embeds the dll, such as
    /// GeometryHelper.Drawing or a Tekla plugin, finds the same ones). A font, a pattern file or a linetype file is looked
    /// for in the folders the user set in Global settings' Folder preference first, then in these, then in the presenter's
    /// own built-in sets. A folder that does not exist is passed over without a word.
    /// </summary>
    public sealed class CadSupportFolders
    {
        private static readonly Lazy<CadSupportFolders> Own = new Lazy<CadSupportFolders>(() =>
            new CadSupportFolders(ProgramFolderOfTheDll())
        );

        /// <summary>Makes the default folders of a root, as a test or a host puts them.</summary>
        /// <param name="programFolder">The folder that holds the Fonts, Hatches and Linetypes folders; null or blank for none.</param>
        public CadSupportFolders(string programFolder)
        {
            ProgramFolder = string.IsNullOrWhiteSpace(programFolder) ? null : programFolder.Trim();
        }

        /// <summary>Gets the default folders: those beside GeometryHelper.Cad.dll.</summary>
        public static CadSupportFolders Default => Own.Value;

        /// <summary>Gets the folder that holds the Fonts, Hatches and Linetypes folders: the folder of GeometryHelper.Cad.dll; null where the dll has no place on disk.</summary>
        public string ProgramFolder { get; }

        /// <summary>Gets the default folder of a kind, as <c>C:\App\Fonts</c>, whether it exists or not; null where there is no root.</summary>
        /// <param name="kind">The kind of support file.</param>
        /// <returns>The folder, or null.</returns>
        public string FolderOf(CadSupportKind kind) =>
            ProgramFolder == null ? null : Path.Combine(ProgramFolder, kind.ToString());

        /// <summary>Gets the default folder of a kind where it exists, else none: a folder that does not exist is skipped silently.</summary>
        /// <param name="kind">The kind of support file.</param>
        /// <returns>The folder in a list of one, or an empty list.</returns>
        public IReadOnlyList<string> ExistingOf(CadSupportKind kind)
        {
            string folder = FolderOf(kind);
            return folder != null && Directory.Exists(folder) ? new[] { folder } : Array.Empty<string>();
        }

        private static string ProgramFolderOfTheDll()
        {
            try
            {
                System.Reflection.Assembly dll = typeof(CadSupportFolders).Assembly;

                // Where the dll was loaded from, not where a host with shadow copying put the copy it runs: the copy is alone in its folder.
                if (Uri.TryCreate(dll.CodeBase, UriKind.Absolute, out Uri origin) && origin.IsFile)
                {
                    string folder = Path.GetDirectoryName(origin.LocalPath);

                    if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
                    {
                        return folder;
                    }
                }

                return string.IsNullOrEmpty(dll.Location) ? null : Path.GetDirectoryName(dll.Location);
            }
            catch (Exception e)
                when (e is ArgumentException || e is NotSupportedException || e is System.Security.SecurityException)
            {
                return null;
            }
        }
    }
}
