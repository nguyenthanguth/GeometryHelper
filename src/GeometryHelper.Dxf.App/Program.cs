using System;
using System.IO;
using GeometryHelper.Dxf.Viewer.Wpf;

namespace GeometryHelper.Dxf.App
{
    /// <summary>
    /// Starts GeometryHelper DXF, the presenter on its own: the window of <see cref="CadAppHost"/>, with no model drawing.
    /// <c>GeometryHelper.Dxf.App.exe plan.dxf</c> opens the window on a drawing, as a file association does; <c>--screenshot
    /// out.png plan.dxf [--size 1600 960]</c> and the other options of <see cref="CadAppHost"/> draw the window or one of its
    /// dialogs into a PNG, with no window shown, and exit: 0 when written, 2 for arguments or a file it cannot read. The
    /// command plugins are kept in <c>%AppData%\GeometryHelper\Dxf</c>.
    /// </summary>
    public static class Program
    {
        /// <summary>The entry point.</summary>
        /// <param name="args">The command-line arguments.</param>
        /// <returns>The exit code.</returns>
        [STAThread]
        public static int Main(string[] args)
        {
            // The code pages of the big fonts (932, 936, 949, 950) and of old DXF files, which .NET (Core) reads only once registered.
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return new CadAppHost("GeometryHelper DXF", Path.Combine(appData, "GeometryHelper", "Dxf")).Run(args);
        }
    }
}
