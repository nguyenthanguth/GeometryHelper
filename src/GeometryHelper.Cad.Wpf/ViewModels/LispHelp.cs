namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// The Help button of Load/Unload Applications and of the Startup Suite: each opens the page of the guide that tells about
    /// it, on the documentation site, in the browser. The page is <c>presenter/lisp.md</c> of the guides, and the heading each
    /// dialog belongs under.
    /// </summary>
    internal static class LispHelp
    {
        private const string Page = "https://nguyenthanguth.github.io/GeometryHelper/guides/presenter/lisp.html";

        /// <summary>The guide's page on AutoLISP, at the heading "APPLOAD": Load/Unload Applications.</summary>
        internal static string AppLoad => Page + "#appload";

        /// <summary>The guide's page at the heading "The Startup Suite": the list of files loaded into every drawing opened.</summary>
        internal static string StartupSuite => Page + "#the-startup-suite";
    }
}
