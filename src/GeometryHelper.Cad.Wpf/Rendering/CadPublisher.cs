using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Media;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>What a publish did: the PDF files written, the sheets printed, the sheets passed over, and one line for each thing that happened, as the command line says them.</summary>
    public sealed class CadPublishResult
    {
        private readonly List<string> _files = new List<string>();
        private readonly List<string> _messages = new List<string>();

        /// <summary>Gets the PDF files written, in the order they were written.</summary>
        public IReadOnlyList<string> Files => _files;

        /// <summary>Gets the lines to tell on the command line, in order: a file written, a sheet printed, a sheet passed over, a file that could not be written.</summary>
        public IReadOnlyList<string> Messages => _messages;

        /// <summary>Gets the number of sheets written to PDF files.</summary>
        public int Written { get; internal set; }

        /// <summary>Gets the number of sheets sent to a printer.</summary>
        public int Printed { get; internal set; }

        /// <summary>Gets the number of sheets passed over or lost to a failure.</summary>
        public int Failed { get; internal set; }

        internal void Say(string message) => _messages.Add(message);

        internal void Wrote(string file) => _files.Add(file);
    }

    /// <summary>
    /// Carries out a publish: the steps of a sheet list (<see cref="CadPublishList.Plan"/>) through the presenter's own
    /// writers. The sheets of a PDF go to <see cref="CadPdfWriter.WritePages(string, IReadOnlyList{CadDocument}, IReadOnlyList{CadPlotJob})"/>,
    /// one file for all of them or a file each, a page the size of its own sheet; a sheet for a printer is drawn as a visual
    /// (<see cref="CadPrintWriter"/>) and handed to the printer, on the sheet of its list nearest the size the page setup asks.
    /// A file or a sheet that fails is told in <see cref="CadPublishResult.Messages"/> and does not stop the sheets after it.
    /// </summary>
    public static class CadPublisher
    {
        /// <summary>
        /// Publishes a sheet list. A list of a drawing open and two layouts, ISO A3 and ISO A1, and a file added with one more
        /// layout is a PDF of three pages of 420 by 297, 841 by 594 and the third's size, when the options say multi-sheet.
        /// </summary>
        /// <param name="list">The sheet list and its options.</param>
        /// <param name="printers">The printers of Windows this computer has, for the sheets whose page setup names one; null for none.</param>
        /// <returns>What was done.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the list is null.</exception>
        public static CadPublishResult Publish(CadPublishList list, IEnumerable<IPlotPrinter> printers)
        {
            if (list == null)
            {
                throw new ArgumentNullException(nameof(list));
            }

            List<IPlotPrinter> known = (printers ?? Array.Empty<IPlotPrinter>()).Where(p => p != null).ToList();
            IReadOnlyList<CadPublishStep> steps = list.Plan(known.Select(p => p.Name));
            var result = new CadPublishResult();
            var written = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (CadPublishStep step in steps)
            {
                if (step.Skipped != null)
                {
                    result.Failed++;
                    result.Say(step.Sheet.Name + ": " + step.Skipped.TrimEnd('.') + ". Not published.");
                }
                else if (step.Printer != null)
                {
                    Print(list, step, known, result);
                }
                else if (written.Add(step.File))
                {
                    WritePdf(
                        step.File,
                        steps.Where(s => s.File != null && SameFile(s.File, step.File)).ToList(),
                        result
                    );
                }
            }

            result.Say(
                "Publish complete: "
                    + Count(result.Written + result.Printed, "sheet")
                    + " published"
                    + (result.Failed == 0 ? string.Empty : ", " + Count(result.Failed, "sheet") + " not published")
                    + "."
            );
            return result;
        }

        private static bool SameFile(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        private static string Count(int n, string what) =>
            n.ToString(CultureInfo.InvariantCulture) + " " + what + (n == 1 ? string.Empty : "s");

        private static void WritePdf(string file, List<CadPublishStep> pages, CadPublishResult result)
        {
            try
            {
                string folder = Path.GetDirectoryName(file);

                if (!string.IsNullOrEmpty(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                CadPdfWriter.WritePages(
                    file,
                    pages.Select(p => p.Sheet.Document).ToList(),
                    pages.Select(p => new CadPlotJob(p.Sheet.Space, p.Settings, DisplayBox.Empty, file)).ToList()
                );
                result.Written += pages.Count;
                result.Wrote(file);
                result.Say("Published " + Count(pages.Count, "sheet") + " to " + file + ".");
            }
            catch (Exception exception) when (IsPublishFailure(exception))
            {
                GeometryHelperLog.Warn("CadPublisher: " + file + " was not written.", exception);
                result.Failed += pages.Count;
                result.Say(file + " was not written: " + exception.Message);
            }
        }

        private static void Print(
            CadPublishList list,
            CadPublishStep step,
            List<IPlotPrinter> printers,
            CadPublishResult result
        )
        {
            CadPublishSheet sheet = step.Sheet;

            try
            {
                IPlotPrinter printer = printers.First(p =>
                    string.Equals(p.Name, step.Printer, StringComparison.OrdinalIgnoreCase)
                );
                IReadOnlyList<CadPaper> papers = printer.GetPapers();
                step.Settings.Paper = PlotDialogViewModel.ClosestPaper(
                    papers == null || papers.Count == 0 ? CadPaper.Standard : papers,
                    step.Settings.Paper.IsPixels ? CadPaper.Default : step.Settings.Paper
                );
                DrawingVisual visual = CadPrintWriter.CreatePage(
                    sheet.Document,
                    sheet.Space,
                    step.Settings,
                    DisplayBox.Empty,
                    out CadPlotPage page
                );
                int copies = list.Options.Copies;
                printer.Print(
                    new PlotPrintJob(sheet.Name, step.Settings.Paper, page.SheetWidth, page.SheetHeight, copies, visual)
                );
                result.Printed++;
                result.Say(
                    "Printed "
                        + sheet.Name
                        + " on "
                        + printer.Name
                        + (
                            copies == 1
                                ? string.Empty
                                : ", " + copies.ToString(CultureInfo.InvariantCulture) + " copies"
                        )
                        + "."
                );
            }
            catch (Exception exception) when (IsPublishFailure(exception))
            {
                GeometryHelperLog.Warn("CadPublisher: " + sheet.Name + " was not printed.", exception);
                result.Failed++;
                result.Say(sheet.Name + " was not printed: " + exception.Message);
            }
        }

        private static bool IsPublishFailure(Exception exception) =>
            exception is InvalidOperationException
            || exception is IOException
            || exception is UnauthorizedAccessException
            || exception is ArgumentException;
    }
}
