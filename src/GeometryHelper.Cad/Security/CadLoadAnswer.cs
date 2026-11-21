namespace GeometryHelper.Cad
{
    /// <summary>
    /// The answer to the warning of SECURELOAD 1, the one AutoCAD shows for a file of code outside the trusted folders
    /// (<see cref="CadLoadSecurity.Ask"/>).
    /// </summary>
    public enum CadLoadAnswer
    {
        /// <summary>Do Not Load: the file is not loaded. It is also the answer to Esc and to closing the warning.</summary>
        DoNotLoad,

        /// <summary>Load Once: the file is loaded, and not asked about again while the app runs.</summary>
        LoadOnce,

        /// <summary>Always Load: the file is loaded, and not asked about again for as long as its bytes stay the same.</summary>
        AlwaysLoad,
    }
}
