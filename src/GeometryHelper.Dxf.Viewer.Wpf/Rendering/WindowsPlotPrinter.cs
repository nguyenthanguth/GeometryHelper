using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Printing;
using System.Text.RegularExpressions;
using System.Windows.Controls;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// A printer of Windows, printed to through WPF's print queues: the sheets it lists come from its capabilities, each with
    /// the margins of its imageable area, and a page goes to it as a visual with a print ticket of the paper, the turn and the
    /// copies, validated by the driver. Nothing here is run by a test, which gives <see cref="IPlotPrinter"/> a queue that
    /// prints nothing.
    /// </summary>
    public sealed class WindowsPlotPrinter : IPlotPrinter
    {
        private const double MillimetresPerUnit = 25.4 / 96.0;

        private readonly string _fullName;

        private WindowsPlotPrinter(string fullName)
        {
            _fullName = fullName;
        }

        /// <inheritdoc/>
        public string Name => _fullName;

        /// <inheritdoc/>
        public bool IsNetwork => _fullName.StartsWith(@"\\", StringComparison.Ordinal);

        /// <summary>
        /// Lists the printers of Windows, local ones and those connected over the network, by name in ordinal order, letters of
        /// either case the same, as the print spooler gives them.
        /// </summary>
        /// <returns>The printers; empty where the spooler is not there or lists none.</returns>
        public static IReadOnlyList<IPlotPrinter> Installed()
        {
            var printers = new List<IPlotPrinter>();

            try
            {
                using (var server = new LocalPrintServer())
                {
                    foreach (PrintQueue queue in server.GetPrintQueues(Kinds))
                    {
                        using (queue)
                        {
                            printers.Add(new WindowsPlotPrinter(queue.FullName));
                        }
                    }
                }
            }
            catch (PrintSystemException exception)
            {
                GeometryHelperLog.Warn("WindowsPlotPrinter: the printers of Windows could not be listed.", exception);
            }

            printers.Sort(
                (a, b) =>
                {
                    int byName = string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                    return byName != 0 ? byName : string.CompareOrdinal(a.Name, b.Name);
                }
            );
            return printers;
        }

        private static readonly EnumeratedPrintQueueTypes[] Kinds =
        {
            EnumeratedPrintQueueTypes.Local,
            EnumeratedPrintQueueTypes.Connections,
        };

        /// <inheritdoc/>
        public IReadOnlyList<CadPaper> GetPapers()
        {
            var papers = new List<CadPaper>();

            try
            {
                using (PrintQueue queue = Open())
                {
                    var sizes = new List<PageMediaSize>();

                    foreach (PageMediaSize size in queue.GetPrintCapabilities().PageMediaSizeCapability)
                    {
                        if (size.Width.HasValue && size.Height.HasValue && size.Width > 0.0 && size.Height > 0.0)
                        {
                            sizes.Add(size);
                        }
                    }

                    foreach (PageMediaSize size in sizes)
                    {
                        double width = Mm(size.Width.Value);
                        double height = Mm(size.Height.Value);

                        // A sheet named on its side (ISOA4Rotated) is the upright one turned: the list keeps the upright one.
                        if (
                            width > height
                            && sizes.Exists(other =>
                                !ReferenceEquals(other, size)
                                && Math.Abs(Mm(other.Width.Value) - height) < 0.5
                                && Math.Abs(Mm(other.Height.Value) - width) < 0.5
                            )
                        )
                        {
                            continue;
                        }

                        string name = NameOf(size, width, height);
                        CadPaper paper = PaperOf(queue, size, name, width, height);

                        if (paper != null && !papers.Exists(p => p.IsSameAs(paper)))
                        {
                            papers.Add(paper);
                        }
                    }
                }
            }
            catch (PrintSystemException exception)
            {
                GeometryHelperLog.Warn(
                    "WindowsPlotPrinter: the sheets of " + _fullName + " could not be listed.",
                    exception
                );
            }

            return papers;
        }

        /// <inheritdoc/>
        public void Print(PlotPrintJob job)
        {
            if (job == null)
            {
                throw new ArgumentNullException(nameof(job));
            }

            try
            {
                using (PrintQueue queue = Open())
                {
                    var wanted = new PrintTicket
                    {
                        PageMediaSize = new PageMediaSize(
                            Math.Min(job.Paper.Width, job.Paper.Height) / MillimetresPerUnit,
                            Math.Max(job.Paper.Width, job.Paper.Height) / MillimetresPerUnit
                        ),
                        PageOrientation =
                            job.SheetWidth > job.SheetHeight ? PageOrientation.Landscape : PageOrientation.Portrait,
                        CopyCount = job.Copies,
                    };
                    PrintTicket ticket = queue
                        .MergeAndValidatePrintTicket(queue.UserPrintTicket, wanted)
                        .ValidatedPrintTicket;
                    var dialog = new PrintDialog { PrintQueue = queue, PrintTicket = ticket };

                    // The dialog is not shown: it is WPF's way to send a visual to a queue under a job name.
                    dialog.PrintVisual(job.Page, job.Title);
                }
            }
            catch (PrintSystemException exception)
            {
                throw new InvalidOperationException(
                    "The printer " + _fullName + " did not take the plot: " + exception.Message,
                    exception
                );
            }
        }

        /// <summary>The queue of this printer, found among the local and connected ones by its name.</summary>
        private PrintQueue Open()
        {
            using (var server = new LocalPrintServer())
            {
                PrintQueue found = null;

                foreach (PrintQueue queue in server.GetPrintQueues(Kinds))
                {
                    if (found == null && string.Equals(queue.FullName, _fullName, StringComparison.OrdinalIgnoreCase))
                    {
                        found = queue;
                    }
                    else
                    {
                        queue.Dispose();
                    }
                }

                return found
                    ?? throw new InvalidOperationException("The printer " + _fullName + " is no longer there.");
            }
        }

        /// <summary>A sheet with the margins of the imageable area the driver gives for it; the margins of 5 where it gives none.</summary>
        private static CadPaper PaperOf(PrintQueue queue, PageMediaSize size, string name, double width, double height)
        {
            double left = 5.0;
            double top = 5.0;
            double right = 5.0;
            double bottom = 5.0;

            try
            {
                var ticket = new PrintTicket { PageMediaSize = size };
                PageImageableArea area = queue.GetPrintCapabilities(ticket).PageImageableArea;

                if (area != null && area.ExtentWidth > 0.0 && area.ExtentHeight > 0.0)
                {
                    left = Math.Max(0.0, area.OriginWidth * MillimetresPerUnit);
                    top = Math.Max(0.0, area.OriginHeight * MillimetresPerUnit);
                    right = Math.Max(0.0, width - left - area.ExtentWidth * MillimetresPerUnit);
                    bottom = Math.Max(0.0, height - top - area.ExtentHeight * MillimetresPerUnit);
                }
            }
            catch (PrintSystemException exception)
            {
                GeometryHelperLog.Debug("WindowsPlotPrinter: no margins for " + name + ": " + exception.Message);
            }

            try
            {
                return new CadPaper(name, width, height, left, bottom, right, top);
            }
            catch (ArgumentException)
            {
                // Margins that leave no room on the sheet: a sheet with margins of 5, or none where the sheet is smaller.
                return width > 10.0 && height > 10.0 ? new CadPaper(name, width, height, 5.0, 5.0, 5.0, 5.0) : null;
            }
        }

        /// <summary>Whole units of 1/96 inch to millimetres, to a hundredth.</summary>
        private static double Mm(double units) => Math.Round(units * MillimetresPerUnit, 2);

        /// <summary>
        /// The sheet's name as the list shows it: the driver's own name spaced out and cleaned (<c>ISOA4</c> as "ISO A4",
        /// <c>NorthAmericaLetter</c> as "Letter"); "Custom 210 x 297" where it has none.
        /// </summary>
        private static string NameOf(PageMediaSize size, double width, double height)
        {
            string raw = size.PageMediaSizeName?.ToString() ?? string.Empty;

            if (raw.Length == 0 || raw == "Unknown")
            {
                return "Custom "
                    + width.ToString("0.##", CultureInfo.InvariantCulture)
                    + " x "
                    + height.ToString("0.##", CultureInfo.InvariantCulture);
            }

            if (raw.StartsWith("NorthAmerica", StringComparison.Ordinal))
            {
                raw = raw.Substring("NorthAmerica".Length);
            }

            // ISOA4 as ISO A4, JISB5 as JIS B5, PRC16K as PRC 16K, and a word run into the next as two.
            string name = Regex.Replace(raw, "(?<=[a-z])(?=[A-Z])", " ");
            name = Regex.Replace(name, "^(ISO|JIS|PRC)(?=[A-Z0-9])", "$1 ");

            return name.Replace("  ", " ").Trim();
        }
    }
}
