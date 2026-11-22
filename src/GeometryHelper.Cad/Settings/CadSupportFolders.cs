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

        /// <summary>AutoLISP files (.lsp) loaded into every drawing at start, and the .dcl files they use.</summary>
        Lisps,

        /// <summary>Command plugins (.dll) loaded at every start, each .dll and each folder of .dll files, at any depth, one plugin; on no search path.</summary>
        Plugins,

        /// <summary>Drawing templates (.dwt, .dwg, .dxf) that NEW lists in Select Template.</summary>
        DrawingTemplates,
    }

    /// <summary>
    /// The default support folders, as AutoCAD's own Fonts and Support folders are: <c>Fonts</c>, <c>Hatches</c>,
    /// <c>Linetypes</c>, <c>Lisps</c>, <c>Plugins</c> and <c>DrawingTemplates</c> beside GeometryHelper.Cad.dll (not beside
    /// the exe, so that a host which embeds the dll, such as a model drawing library or a Tekla plugin, finds the same ones).
    /// A font, a pattern file or a linetype file is looked for in the folders the user set in Global settings' Folder
    /// preference first, then in these and their subfolders, then in the presenter's own built-in sets. Every folder is read
    /// the same way: depth first, a folder's own files by name first and then its subfolders in the same order, so that a
    /// file at the top level comes before one in a subfolder and, of two files of one name, the first wins; a subfolder that
    /// is a junction or a symbolic link, or is hidden or a system folder, is not entered. Every <c>.lsp</c> file in
    /// <c>Lisps</c>, at any depth, is loaded into each drawing when it opens, and the folder itself (not its subfolders) is
    /// on the AutoLISP search path after the user's folders. Every <c>.dll</c> directly in <c>Plugins</c> is one command
    /// plugin, and so is every folder at any depth that holds <c>.dll</c> files directly (the folders below it are not
    /// walked); a folder with none is walked into, so that <c>Plugins\Company\ToolA\ToolA.dll</c> is the plugin ToolA. They
    /// are loaded once when the app starts; the folder is on no search path. The templates in <c>DrawingTemplates</c> and its
    /// subfolders are the ones NEW lists where Global settings name no template folder. A folder that does not exist is
    /// passed over without a word.
    /// </summary>
    public sealed class CadSupportFolders
    {
        private static readonly Lazy<CadSupportFolders> Own = new Lazy<CadSupportFolders>(() =>
            new CadSupportFolders(ProgramFolderOfTheDll())
        );

        /// <summary>Makes the default folders of a root, as a test or a host puts them.</summary>
        /// <param name="programFolder">The folder that holds the Fonts, Hatches, Linetypes, Lisps, Plugins and DrawingTemplates folders; null or blank for none.</param>
        public CadSupportFolders(string programFolder)
        {
            ProgramFolder = string.IsNullOrWhiteSpace(programFolder) ? null : programFolder.Trim();
        }

        /// <summary>Gets the default folders: those beside GeometryHelper.Cad.dll.</summary>
        public static CadSupportFolders Default => Own.Value;

        /// <summary>Gets the folder that holds the Fonts, Hatches, Linetypes, Lisps, Plugins and DrawingTemplates folders: the folder of GeometryHelper.Cad.dll; null where the dll has no place on disk.</summary>
        public string ProgramFolder { get; }

        /// <summary>Gets the default folder of a kind, as <c>C:\App\Fonts</c>, whether it exists or not; null where there is no root.</summary>
        /// <param name="kind">The kind of support file.</param>
        /// <returns>The folder, or null.</returns>
        public string FolderOf(CadSupportKind kind) =>
            ProgramFolder == null ? null : Path.Combine(ProgramFolder, kind.ToString());

        /// <summary>Gets the default folder of a kind where it exists, else none: a folder that does not exist is skipped silently; its subfolders are not listed.</summary>
        /// <param name="kind">The kind of support file.</param>
        /// <returns>The folder in a list of one, or an empty list.</returns>
        public IReadOnlyList<string> ExistingOf(CadSupportKind kind)
        {
            string folder = FolderOf(kind);
            return folder != null && Directory.Exists(folder) ? new[] { folder } : Array.Empty<string>();
        }

        /// <summary>
        /// The default folder of a kind and the subfolders of it that are walked (<see cref="CadSupportWalk"/>), in the order of
        /// the walk, the folder first: where a font, a pattern, a linetype, a template or a lisp is looked for. None where the
        /// folder does not exist. The AutoLISP search path and the Plugins folder do not use it.
        /// </summary>
        internal IReadOnlyList<string> SearchFoldersOf(CadSupportKind kind)
        {
            string folder = FolderOf(kind);
            return folder != null && Directory.Exists(folder) ? CadSupportWalk.Folders(folder) : Array.Empty<string>();
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
