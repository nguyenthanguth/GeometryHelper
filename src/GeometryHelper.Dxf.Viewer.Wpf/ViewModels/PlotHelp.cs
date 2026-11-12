using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The Help button of the plot dialogs: each opens the page of the guide that tells about it, on the documentation site,
    /// in the browser. The page is <c>presenter/plot.md</c> of the guides, and the heading each dialog belongs under.
    /// </summary>
    internal static class PlotHelp
    {
        private const string Page = "https://nguyenthanguth.github.io/GeometryHelper/guides/presenter/plot.html";

        /// <summary>The guide's page on plotting, at the heading "The page setup": the Plot dialog and the Page Setup form.</summary>
        internal static string PlotDialog => Page + "#the-page-setup";

        /// <summary>The guide's page at the heading "Named page setups": the Page Setup Manager and the dialogs it opens.</summary>
        internal static string PageSetups => Page + "#named-page-setups";

        /// <summary>Opens a page in the browser; a browser that cannot be started is logged and left, never thrown.</summary>
        /// <param name="url">The address.</param>
        internal static void Open(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception exception)
                when (exception is Win32Exception
                    || exception is InvalidOperationException
                    || exception is FileNotFoundException
                )
            {
                GeometryHelperLog.Warn("PlotHelp: " + url + " could not be opened.", exception);
            }
        }
    }
}
