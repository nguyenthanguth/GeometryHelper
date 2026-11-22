namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// The link under the load warning opens the page of the guide that tells about it, on the documentation site, in the
    /// browser. The page is <c>presenter/lisp.md</c> of the guides, and the heading the warning belongs under.
    /// </summary>
    internal static class LispHelp
    {
        private const string Page = "https://nguyenthanguth.github.io/GeometryHelper/guides/presenter/lisp.html";

        /// <summary>The guide's page at the heading "Trusted folders and SECURELOAD": which files may load, and the warning that asks about the others.</summary>
        internal static string TrustedFolders => Page + "#trusted-folders-and-secureload";
    }
}
