namespace GeometryHelper.Cad
{
    /// <summary>
    /// What <c>getfiled</c> hands the window with the GETFILED request: the dialog's title, the starting name, the extensions
    /// and AutoCAD's flags. The window puts the path chosen in <see cref="CadUiRequestEventArgs.Answer"/>, and leaves it null
    /// when the user gives up.
    /// </summary>
    public sealed class CadGetFileRequest
    {
        internal CadGetFileRequest(string title, string defaultName, string extension, int flags)
        {
            Title = title;
            DefaultName = defaultName;
            Extension = extension;
            Flags = flags;
        }

        /// <summary>Gets the title of the dialog.</summary>
        public string Title { get; }

        /// <summary>Gets the file or folder the dialog starts at, as the routine gave it; empty for none.</summary>
        public string DefaultName { get; }

        /// <summary>Gets the extensions offered, without dots, separated by semicolons (<c>"dwg;dxf"</c>); empty for any file.</summary>
        public string Extension { get; }

        /// <summary>
        /// Gets the flags as AutoCAD reads them: 1 a new file (a save dialog), 4 any extension, 8 the default searched in the
        /// library path, 16 the default is a folder, 32 no question before overwriting, 64 and 128 the Internet options.
        /// </summary>
        public int Flags { get; }
    }
}
