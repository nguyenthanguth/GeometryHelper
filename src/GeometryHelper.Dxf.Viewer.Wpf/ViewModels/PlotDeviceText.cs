using System;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The words that tell what a plotter is, where it writes and what it does, shared by the Plot dialog's Printer/plotter box
    /// and the details of a page setup in the Page Setup Manager and Import Page Setups.
    /// </summary>
    internal static class PlotDeviceText
    {
        /// <summary>What a plotter is: "GeometryHelper PDF · vector, built in", "Office printer · printer of Windows".</summary>
        internal static string Plotter(string device, bool isRaster, bool isPrinter) =>
            device
            + (
                isPrinter ? " · printer of Windows"
                : isRaster ? " · raster, built in"
                : " · vector, built in"
            );

        /// <summary>
        /// Where a plotter writes: File for the built-in ones; for a printer of Windows its port or share where it says
        /// (<c>USB001</c>, <c>\\office-srv\Office printer</c>), else Printer, or Network printer when it is on the network.
        /// </summary>
        internal static string Where(IPlotPrinter printer)
        {
            if (printer == null)
            {
                return "File";
            }

            string port = (printer as IPlotPrinterPort)?.Port;
            return string.IsNullOrWhiteSpace(port) ? (printer.IsNetwork ? "Network printer" : "Printer") : port.Trim();
        }

        /// <summary>What a plotter does, in a sentence.</summary>
        internal static string Description(string device, bool isPrinter) =>
            isPrinter ? "Prints on paper through Windows, drawn at up to 300 dots to the inch."
            : string.Equals(device, CadPlotSettings.PngDevice, StringComparison.OrdinalIgnoreCase)
                ? "Writes a PNG image, lossless, one file per plot."
            : string.Equals(device, CadPlotSettings.JpgDevice, StringComparison.OrdinalIgnoreCase)
                ? "Writes a JPEG image, quality 90, one file per plot; no transparency."
            : "Writes a PDF, one page per plot; text stays searchable.";

        /// <summary>
        /// The hint after a printer's name in the Printer/plotter list: "Windows · network" for one on the network,
        /// "Windows · USB" for one on a USB port, else "Windows".
        /// </summary>
        internal static string Hint(IPlotPrinter printer)
        {
            if (printer.IsNetwork)
            {
                return "Windows · network";
            }

            string port = (printer as IPlotPrinterPort)?.Port ?? string.Empty;
            return port.TrimStart().StartsWith("USB", StringComparison.OrdinalIgnoreCase) ? "Windows · USB" : "Windows";
        }
    }
}
