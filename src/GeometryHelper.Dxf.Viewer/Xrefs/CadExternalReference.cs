using System;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>Where an external reference or an image of a drawing stands, as AutoCAD's External References palette says it.</summary>
    public enum CadReferenceStatus
    {
        /// <summary>Its file was found and read, and is shown.</summary>
        Loaded,

        /// <summary>Unloaded by -XREF Unload, -IMAGE Unload or the palette: not shown until reloaded; its INSERTs and images stay.</summary>
        Unloaded,

        /// <summary>No file was found where its path, the drawing's folder or the support folders say.</summary>
        NotFound,

        /// <summary>Its file was found but could not be read as a drawing.</summary>
        Unresolved,

        /// <summary>No INSERT places it, or no image shows it.</summary>
        Unreferenced,

        /// <summary>Its file changed on disk since it was read: Reload reads it again.</summary>
        NeedsReloading,

        /// <summary>A nested reference whose parent is unloaded, not found or unresolved, so that it is not shown.</summary>
        Orphaned,
    }

    /// <summary>How the path of an external reference or an image is written, as AutoCAD's Path type names it.</summary>
    public enum CadPathType
    {
        /// <summary>From the folder of the drawing, as <c>..\xref\grid.dxf</c>; the full path while the drawing has no file.</summary>
        Relative,

        /// <summary>The full path, as <c>D:\Plans\xref\grid.dxf</c>.</summary>
        Full,

        /// <summary>The file's name alone, as <c>grid.dxf</c>, found in the drawing's folder or a support folder.</summary>
        None,
    }

    /// <summary>
    /// One row of AutoCAD's External References palette: an external reference (a DXF attached or overlaid) or an image
    /// definition of the drawing, with where it stands and the file found for it.
    /// </summary>
    public sealed class CadExternalReference
    {
        internal CadExternalReference(string name, bool isImage, bool isOverlay, string savedPath, string foundAt, CadReferenceStatus status, string parentName = null, int depth = 0)
        {
            Name = name;
            ParentName = parentName;
            Depth = depth;
            IsImage = isImage;
            IsOverlay = isOverlay;
            SavedPath = savedPath ?? string.Empty;
            FoundAt = foundAt;
            Status = status;

            if (foundAt != null)
            {
                try
                {
                    var info = new System.IO.FileInfo(foundAt);
                    Size = info.Length;
                    Date = info.LastWriteTime;
                }
                catch (Exception exception) when (exception is System.IO.IOException || exception is UnauthorizedAccessException || exception is ArgumentException || exception is NotSupportedException)
                {
                    Size = -1;
                }
            }
            else
            {
                Size = -1;
            }
        }

        /// <summary>Gets the name: the block of an external reference, the entry of an image definition.</summary>
        public string Name { get; }

        /// <summary>Gets whether it is an image rather than an external reference.</summary>
        public bool IsImage { get; }

        /// <summary>
        /// Gets the name of the external reference whose drawing places it, for a nested reference; null for one the drawing
        /// places itself, or an image.
        /// </summary>
        public string ParentName { get; }

        /// <summary>Gets how deep it is nested: 0 for one the drawing places, 1 for one placed by such a reference's drawing, and on.</summary>
        public int Depth { get; }

        /// <summary>Gets whether it is a nested reference: one the drawing of another reference places.</summary>
        public bool IsNested => Depth > 0;

        /// <summary>Gets whether an external reference is overlaid rather than attached.</summary>
        public bool IsOverlay { get; }

        /// <summary>Gets the path the drawing names the file by, as it is written.</summary>
        public string SavedPath { get; }

        /// <summary>Gets the full path of the file found; null where none was.</summary>
        public string FoundAt { get; }

        /// <summary>Gets where it stands.</summary>
        public CadReferenceStatus Status { get; }

        /// <summary>Gets the size of the file found in bytes; -1 where none was found.</summary>
        public long Size { get; }

        /// <summary>Gets the time the file found was last written, in local time; null where none was found.</summary>
        public DateTime? Date { get; }

        /// <summary>Gets how its path is written: a full path, one from the drawing's folder, or the file's name alone.</summary>
        public CadPathType PathType => CadXrefManager.PathTypeOf(SavedPath);

        /// <summary>Gets the type the palette shows: Attach or Overlay for an external reference, the kind of file for an image (PNG, JPG, BMP).</summary>
        public string TypeName
        {
            get
            {
                if (!IsImage)
                {
                    return IsOverlay ? "Overlay" : "Attach";
                }

                string extension = string.Empty;

                try
                {
                    extension = System.IO.Path.GetExtension(SavedPath).TrimStart('.').ToUpperInvariant();
                }
                catch (ArgumentException)
                {
                    extension = string.Empty;
                }

                return extension == "JPEG" ? "JPG" : extension;
            }
        }

        /// <summary>Gets the status in the words of AutoCAD 2021's help of the palette: Loaded, Unloaded, Not Found, Unresolved, Unreferenced, Needs Reloading, Orphaned.</summary>
        public string StatusText
        {
            get
            {
                switch (Status)
                {
                    case CadReferenceStatus.NotFound:
                        return "Not Found";
                    case CadReferenceStatus.NeedsReloading:
                        return "Needs Reloading";
                    default:
                        return Status.ToString();
                }
            }
        }

        /// <inheritdoc/>
        public override string ToString() => Name + " " + StatusText;
    }
}
