using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The external references and images of a document as the presenter shows them: each file found where AutoCAD looks
    /// for it, an external reference read once and drawn as the content of its block, and what was unloaded. Nothing it reads
    /// is written into the drawing but by -XREF Bind (<see cref="DxfDatabase.BindXref"/>): an external reference is shown, and
    /// saving writes its block and INSERTs alone, as AutoCAD writes them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A file is looked for as AutoCAD looks: the path the drawing saved, from the folder of the drawing that names it where
    /// the path is not full; then the file's name in that folder; then in the folder of the document's drawing; then in each
    /// of the document's <see cref="CadDocument.SupportFolders"/>. The reference <c>..\xref\grid.dxf</c> of a drawing in
    /// <c>D:\Plans\Workshop A</c> is found at <c>D:\Plans\xref\grid.dxf</c>, and where that is gone, at
    /// <c>D:\Plans\Workshop A\grid.dxf</c>.
    /// </para>
    /// <para>
    /// An external reference read keeps the time and size its file had, so that one changed on disk since stands as
    /// <see cref="CadReferenceStatus.NeedsReloading"/> until Reload reads it again. Unloading is the presenter's own and is not
    /// saved: no group of a DXF file says an external reference is unloaded.
    /// </para>
    /// </remarks>
    public sealed class CadXrefManager : IDisposable
    {
        private readonly CadDocument _document;

        // Read files by full path, and the names unloaded: looked up only, never listed.
        private readonly Dictionary<string, CadXrefContent> _loaded = new Dictionary<string, CadXrefContent>(
            StringComparer.OrdinalIgnoreCase
        );
        private readonly HashSet<string> _unloadedXrefs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, bool> _imageLoaded = new Dictionary<string, bool>(
            StringComparer.OrdinalIgnoreCase
        );

        // The files being drawn, the outermost first, so that a reference to a drawing being drawn is passed over.
        private readonly List<string> _drawing = new List<string>();

        // A watcher of each folder a file read is in: looked up only, never listed.
        private readonly Dictionary<string, FileSystemWatcher> _watchers = new Dictionary<string, FileSystemWatcher>(
            StringComparer.OrdinalIgnoreCase
        );
        private readonly object _watchLock = new object();
        private bool _watch;

        internal CadXrefManager(CadDocument document)
        {
            _document = document;
        }

        /// <summary>
        /// Raised when a file an external reference was read from, or an image shows, changes on disk, while
        /// <see cref="WatchFiles"/> is on; raised on a thread of the watcher, not the window's.
        /// </summary>
        public event EventHandler FilesChanged;

        /// <summary>Gets or sets whether the folders of the files read are watched, so that <see cref="FilesChanged"/> is raised; off until set, as in a test.</summary>
        public bool WatchFiles
        {
            get => _watch;
            set
            {
                _watch = value;

                if (!value)
                {
                    StopWatching();
                    return;
                }

                foreach (
                    CadXrefContent content in _loaded.Values.OrderBy(c => c.FullPath, StringComparer.OrdinalIgnoreCase)
                )
                {
                    Watch(content.FullPath);
                }
            }
        }

        /// <summary>
        /// Gets the external references of the drawing, then its images, each in ordinal order of its name, letters of either
        /// case alike first, with where it stands: an external reference no INSERT places is Unreferenced, one whose file is not
        /// found Not Found, and so on. An external reference not yet read is read here. Each one the drawing places is followed
        /// by the references its drawing attaches, nested, as the palette's tree shows them (<see cref="CadExternalReference.Depth"/>,
        /// <see cref="CadExternalReference.ParentName"/>), each in the order of its name; a nested overlay is not, as AutoCAD
        /// shows none. A nested reference whose parent is unloaded, not found or unresolved is Orphaned. The record AutoCAD
        /// keeps in the drawing of a nested reference, which no INSERT of the drawing places, is listed under its parent alone,
        /// not as an Unreferenced reference of its own.
        /// </summary>
        /// <returns>The rows of the palette.</returns>
        public IReadOnlyList<CadExternalReference> GetReferences()
        {
            var references = new List<CadExternalReference>();
            DxfDatabase database = _document.Database;
            var blocks = new List<DxfBlockTableRecord>();

            foreach (DxfBlockTableRecord block in database.BlockTable)
            {
                if (block.IsFromExternalReference && !block.IsLayout)
                {
                    blocks.Add(block);
                }
            }

            blocks.Sort((a, b) => ByName(a.Name, b.Name));
            var rows = new List<KeyValuePair<CadExternalReference, List<CadExternalReference>>>();
            var nestedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (DxfBlockTableRecord block in blocks)
            {
                string found = Locate(block.PathName, _document.DrawingFolder);
                var row = new CadExternalReference(
                    block.Name,
                    false,
                    block.IsFromOverlayReference,
                    block.PathName,
                    found,
                    StatusOf(block, found)
                );
                var nested = new List<CadExternalReference>();

                if (row.Status != CadReferenceStatus.Unreferenced && found != null)
                {
                    AddNested(nested, block.Name, found, row.Status, 1, new List<string> { found });
                }

                foreach (CadExternalReference child in nested)
                {
                    nestedNames.Add(child.Name);
                }

                rows.Add(new KeyValuePair<CadExternalReference, List<CadExternalReference>>(row, nested));
            }

            foreach (KeyValuePair<CadExternalReference, List<CadExternalReference>> row in rows)
            {
                if (row.Key.Status == CadReferenceStatus.Unreferenced && nestedNames.Contains(row.Key.Name))
                {
                    continue;
                }

                references.Add(row.Key);
                references.AddRange(row.Value);
            }

            var images = new List<KeyValuePair<string, DxfRasterImageDef>>();
            DxfDBDictionary dictionary = Images(database);

            if (dictionary != null)
            {
                foreach (DxfDBDictionaryEntry entry in dictionary)
                {
                    if (entry.Value is DxfRasterImageDef definition)
                    {
                        images.Add(new KeyValuePair<string, DxfRasterImageDef>(entry.Key, definition));
                    }
                }
            }

            images.Sort((a, b) => ByName(a.Key, b.Key));

            foreach (KeyValuePair<string, DxfRasterImageDef> image in images)
            {
                string found = Locate(image.Value.SourceFileName, _document.DrawingFolder);
                CadReferenceStatus status =
                    image.Value.GetEntityCount(out _) == 0 ? CadReferenceStatus.Unreferenced
                    : !IsImageLoaded(image.Key, image.Value) ? CadReferenceStatus.Unloaded
                    : found == null ? CadReferenceStatus.NotFound
                    : CadReferenceStatus.Loaded;
                references.Add(
                    new CadExternalReference(image.Key, true, false, image.Value.SourceFileName, found, status)
                );
            }

            return references;
        }

        /// <summary>Finds the file a saved path names, as AutoCAD looks for it from the document's drawing (see the remarks of the class).</summary>
        /// <param name="savedPath">The path as the drawing writes it.</param>
        /// <returns>The full path of the file found; null where none was.</returns>
        public string Locate(string savedPath) => Locate(savedPath, _document.DrawingFolder);

        /// <summary>Gets whether an external reference of a name is unloaded.</summary>
        /// <param name="name">The name of its block.</param>
        /// <returns>True where -XREF Unload or the palette unloaded it.</returns>
        public bool IsUnloaded(string name) => name != null && _unloadedXrefs.Contains(name);

        /// <summary>
        /// Gets the path to write for a file, as AutoCAD's Path type writes it: Full the full path; None the file's name; Relative
        /// the way from the drawing's folder, <c>.\grid.dxf</c> beside it and <c>..\xref\grid.dxf</c> up one, the full path
        /// where the drawing has no folder yet (it is not saved) or the file is on another drive.
        /// </summary>
        /// <param name="fullPath">The full path of the file.</param>
        /// <param name="type">How the path is to be written.</param>
        /// <param name="drawingFolder">The folder of the drawing; null for a drawing not saved.</param>
        /// <returns>The path.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the path is null.</exception>
        public static string PathFor(string fullPath, CadPathType type, string drawingFolder)
        {
            if (fullPath == null)
            {
                throw new ArgumentNullException(nameof(fullPath));
            }

            if (type == CadPathType.None)
            {
                return Path.GetFileName(fullPath);
            }

            if (type == CadPathType.Full || string.IsNullOrEmpty(drawingFolder))
            {
                return fullPath;
            }

            string[] from = Path.GetFullPath(drawingFolder).TrimEnd('\\', '/').Split('\\', '/');
            string[] to = Path.GetFullPath(fullPath).Split('\\', '/');

            if (!string.Equals(from[0], to[0], StringComparison.OrdinalIgnoreCase))
            {
                return fullPath;
            }

            int same = 0;

            while (
                same < from.Length
                && same < to.Length - 1
                && string.Equals(from[same], to[same], StringComparison.OrdinalIgnoreCase)
            )
            {
                same++;
            }

            var parts = new List<string>();

            for (int i = same; i < from.Length; i++)
            {
                parts.Add("..");
            }

            if (parts.Count == 0)
            {
                parts.Add(".");
            }

            for (int i = same; i < to.Length; i++)
            {
                parts.Add(to[i]);
            }

            return string.Join("\\", parts);
        }

        /// <summary>Gets how a saved path is written: Full where it is rooted, None for a file's name alone, Relative otherwise.</summary>
        /// <param name="savedPath">The path.</param>
        /// <returns>The path type.</returns>
        public static CadPathType PathTypeOf(string savedPath)
        {
            if (string.IsNullOrEmpty(savedPath))
            {
                return CadPathType.None;
            }

            try
            {
                if (Path.IsPathRooted(savedPath))
                {
                    return CadPathType.Full;
                }
            }
            catch (ArgumentException)
            {
                return CadPathType.None;
            }

            return savedPath.IndexOf('\\') >= 0 || savedPath.IndexOf('/') >= 0
                ? CadPathType.Relative
                : CadPathType.None;
        }

        /// <summary>Stops watching every folder.</summary>
        public void Dispose()
        {
            _watch = false;
            StopWatching();
        }

        /// <summary>Unloads an external reference of a name, or loads it again; the display is to be made again after.</summary>
        internal void SetUnloaded(string name, bool unloaded)
        {
            if (unloaded)
            {
                _unloadedXrefs.Add(name);
            }
            else
            {
                _unloadedXrefs.Remove(name);
            }
        }

        /// <summary>Forgets a file read, so that it is read again when next drawn: Reload.</summary>
        internal void Forget(string fullPath)
        {
            if (fullPath != null)
            {
                _loaded.Remove(fullPath);
            }
        }

        /// <summary>Unloads an image definition of a name, or loads it again.</summary>
        internal void SetImageLoaded(string name, bool loaded) => _imageLoaded[name] = loaded;

        /// <summary>Whether the picture of an image definition is shown: as the presenter last set it, else as the drawing saved it (280).</summary>
        internal bool IsImageLoaded(string name, DxfRasterImageDef definition) =>
            name != null && _imageLoaded.TryGetValue(name, out bool loaded) ? loaded : definition.IsLoaded;

        /// <summary>The name an image definition has in the drawing's dictionary of images; null for one not in it.</summary>
        internal static string ImageName(DxfDatabase database, DxfRasterImageDef definition)
        {
            DxfDBDictionary dictionary = Images(database);
            return dictionary == null || definition == null || !dictionary.Contains(definition)
                ? null
                : dictionary.NameAt(definition);
        }

        /// <summary>The dictionary of images of a drawing; null where it has none.</summary>
        internal static DxfDBDictionary Images(DxfDatabase database)
        {
            try
            {
                return DxfRasterImageDef.GetImageDictionary(database);
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        /// <summary>
        /// The drawing an INSERT of an external reference shows: its file found from the folder of the drawing that names it and
        /// read once; null where it is unloaded, not found or cannot be read, <paramref name="missing"/> then the path to show
        /// for one not found or not read, null for one unloaded.
        /// </summary>
        internal CadXrefContent ContentOf(DxfBlockTableRecord block, string referrerFolder, out string missing)
        {
            missing = null;

            if (_unloadedXrefs.Contains(block.Name))
            {
                return null;
            }

            string found = Locate(block.PathName, referrerFolder);

            if (found == null)
            {
                missing = block.PathName;
                return null;
            }

            CadXrefContent content = Read(found);

            if (content.Database == null)
            {
                missing = block.PathName;
                return null;
            }

            return content;
        }

        /// <summary>Marks a file as being drawn; false where it is being drawn already, as a reference to a drawing that references it back.</summary>
        internal bool Enter(CadXrefContent content)
        {
            if (_drawing.Contains(content.FullPath, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }

            _drawing.Add(content.FullPath);
            return true;
        }

        /// <summary>Marks the file drawn last as drawn.</summary>
        internal void Leave() => _drawing.RemoveAt(_drawing.Count - 1);

        /// <summary>The folder of the document's drawing, which a file named from another folder is also looked for in.</summary>
        internal string DrawingFolder => _document.DrawingFolder;

        /// <summary>The content of a file, read once, or again once it changed and was forgotten.</summary>
        internal CadXrefContent Read(string fullPath)
        {
            if (_loaded.TryGetValue(fullPath, out CadXrefContent content))
            {
                return content;
            }

            content = new CadXrefContent(fullPath);
            _loaded[fullPath] = content;

            if (_watch)
            {
                Watch(fullPath);
            }

            return content;
        }

        /// <summary>
        /// The references the drawing of a file attaches, each followed by its own, depth first: Orphaned under a parent not
        /// shown, else as its file is found and read. A file already on the way down is not followed again.
        /// </summary>
        private void AddNested(
            List<CadExternalReference> rows,
            string parent,
            string parentFile,
            CadReferenceStatus parentStatus,
            int depth,
            List<string> chain
        )
        {
            DxfDatabase database = Read(parentFile).Database;

            if (database == null)
            {
                return;
            }

            bool shown = parentStatus == CadReferenceStatus.Loaded || parentStatus == CadReferenceStatus.NeedsReloading;
            string folder = Path.GetDirectoryName(parentFile);
            var blocks = new List<DxfBlockTableRecord>();

            foreach (DxfBlockTableRecord block in database.BlockTable)
            {
                if (
                    block.IsFromExternalReference
                    && !block.IsLayout
                    && !block.IsFromOverlayReference
                    && block.GetBlockReferences(true).Count > 0
                )
                {
                    blocks.Add(block);
                }
            }

            blocks.Sort((a, b) => ByName(a.Name, b.Name));

            foreach (DxfBlockTableRecord block in blocks)
            {
                string found = Locate(block.PathName, folder);
                CadXrefContent content = found == null ? null : Read(found);
                CadReferenceStatus status =
                    !shown ? CadReferenceStatus.Orphaned
                    : _unloadedXrefs.Contains(block.Name) ? CadReferenceStatus.Unloaded
                    : found == null ? CadReferenceStatus.NotFound
                    : content.Database == null ? CadReferenceStatus.Unresolved
                    : content.HasChanged ? CadReferenceStatus.NeedsReloading
                    : CadReferenceStatus.Loaded;
                rows.Add(
                    new CadExternalReference(block.Name, false, false, block.PathName, found, status, parent, depth)
                );

                if (
                    found != null
                    && content.Database != null
                    && !chain.Contains(found, StringComparer.OrdinalIgnoreCase)
                )
                {
                    chain.Add(found);
                    AddNested(rows, block.Name, found, status, depth + 1, chain);
                    chain.RemoveAt(chain.Count - 1);
                }
            }
        }

        private CadReferenceStatus StatusOf(DxfBlockTableRecord block, string found)
        {
            if (block.GetBlockReferences(true).Count == 0)
            {
                return CadReferenceStatus.Unreferenced;
            }

            if (_unloadedXrefs.Contains(block.Name))
            {
                return CadReferenceStatus.Unloaded;
            }

            if (found == null)
            {
                return CadReferenceStatus.NotFound;
            }

            CadXrefContent content = Read(found);

            if (content.Database == null)
            {
                return CadReferenceStatus.Unresolved;
            }

            return content.HasChanged ? CadReferenceStatus.NeedsReloading : CadReferenceStatus.Loaded;
        }

        /// <summary>Finds a file as the remarks of the class say: the path saved, then its name in the folder of the drawing that names it, the document's folder and the support folders.</summary>
        internal string Locate(string savedPath, string referrerFolder)
        {
            if (string.IsNullOrWhiteSpace(savedPath))
            {
                return null;
            }

            var candidates = new List<string>();

            try
            {
                string name = Path.GetFileName(savedPath);

                if (Path.IsPathRooted(savedPath))
                {
                    candidates.Add(savedPath);
                }
                else if (referrerFolder != null)
                {
                    candidates.Add(Path.Combine(referrerFolder, savedPath));
                }

                if (referrerFolder != null)
                {
                    candidates.Add(Path.Combine(referrerFolder, name));
                }

                string own = _document.DrawingFolder;

                if (own != null)
                {
                    candidates.Add(Path.Combine(own, name));
                }

                foreach (string folder in _document.SupportFolders)
                {
                    candidates.Add(Path.Combine(folder, name));
                }

                foreach (string candidate in candidates)
                {
                    if (File.Exists(candidate))
                    {
                        return Path.GetFullPath(candidate);
                    }
                }
            }
            catch (Exception exception)
                when (exception is ArgumentException
                    || exception is NotSupportedException
                    || exception is PathTooLongException
                    || exception is IOException
                    || exception is UnauthorizedAccessException
                )
            {
                // A path with a character no path may hold names no file.
                return null;
            }

            return null;
        }

        private static int ByName(string a, string b)
        {
            int byName = string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
            return byName != 0 ? byName : string.CompareOrdinal(a, b);
        }

        private void Watch(string fullPath)
        {
            string folder;

            try
            {
                folder = Path.GetDirectoryName(fullPath);
            }
            catch (ArgumentException)
            {
                return;
            }

            if (string.IsNullOrEmpty(folder))
            {
                return;
            }

            lock (_watchLock)
            {
                if (_watchers.ContainsKey(folder))
                {
                    return;
                }

                try
                {
                    var watcher = new FileSystemWatcher(folder)
                    {
                        NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                    };
                    watcher.Changed += OnFileChanged;
                    watcher.Created += OnFileChanged;
                    watcher.Renamed += OnFileChanged;
                    watcher.EnableRaisingEvents = true;
                    _watchers[folder] = watcher;
                }
                catch (Exception exception)
                    when (exception is ArgumentException
                        || exception is IOException
                        || exception is PlatformNotSupportedException
                    )
                {
                    GeometryHelperLog.Warn("CadXrefManager: the folder " + folder + " cannot be watched.", exception);
                }
            }
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            if (CadDrawingFile.IsDrawingName(e.FullPath) || CadImageFile.IsPicture(e.FullPath))
            {
                FilesChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void StopWatching()
        {
            lock (_watchLock)
            {
                foreach (FileSystemWatcher watcher in _watchers.Values)
                {
                    watcher.EnableRaisingEvents = false;
                    watcher.Dispose();
                }

                _watchers.Clear();
            }
        }
    }

    /// <summary>A file an external reference was read from: its drawing, or null with why where it could not be read, and its time and size then.</summary>
    internal sealed class CadXrefContent
    {
        internal CadXrefContent(string fullPath)
        {
            FullPath = fullPath;

            try
            {
                var info = new FileInfo(fullPath);
                WriteTime = info.LastWriteTimeUtc;
                Length = info.Length;
                Database = CadDrawingFile.Open(fullPath);
                GeometryHelperLog.Info("CadXrefManager: read the external reference " + fullPath + ".");
            }
            catch (Exception exception)
            {
                // A file that cannot be read stands unresolved, as AutoCAD's "Unable to load".
                Error = exception.Message;
                Database = null;
                GeometryHelperLog.Warn(
                    "CadXrefManager: the external reference " + fullPath + " cannot be read.",
                    exception
                );
            }
        }

        internal string FullPath { get; }

        internal DxfDatabase Database { get; }

        internal string Error { get; }

        internal DateTime WriteTime { get; }

        internal long Length { get; }

        /// <summary>Whether the file's time or size differs from those it had when read.</summary>
        internal bool HasChanged
        {
            get
            {
                try
                {
                    var info = new FileInfo(FullPath);
                    return !info.Exists || info.LastWriteTimeUtc != WriteTime || info.Length != Length;
                }
                catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
                {
                    return true;
                }
            }
        }
    }
}
