using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// A page for a printer: the plot drawn on its sheet as a WPF visual, with the paper it is printed on and how many copies.
    /// The visual stands in device-independent units (96 to the inch), its origin the sheet's upper left corner, the sheet as
    /// it lies (<see cref="SheetWidth"/> by <see cref="SheetHeight"/>); a printer turns it to the paper it is given.
    /// </summary>
    public sealed class PlotPrintJob
    {
        /// <summary>Makes a job.</summary>
        /// <param name="title">The name the job goes by in the printer's queue.</param>
        /// <param name="paper">The sheet of the printer's list the plot is printed on, as the printer names it.</param>
        /// <param name="sheetWidth">The width of the sheet as it lies, in millimetres.</param>
        /// <param name="sheetHeight">The height of the sheet as it lies, in millimetres.</param>
        /// <param name="copies">The number of copies, 1 to 999.</param>
        /// <param name="page">The plot drawn on its sheet.</param>
        /// <exception cref="ArgumentNullException">Thrown when the title, the paper or the page is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a size is not above nought or the copies are not 1 to 999.</exception>
        public PlotPrintJob(
            string title,
            CadPaper paper,
            double sheetWidth,
            double sheetHeight,
            int copies,
            DrawingVisual page
        )
        {
            Title = title ?? throw new ArgumentNullException(nameof(title));
            Paper = paper ?? throw new ArgumentNullException(nameof(paper));
            Page = page ?? throw new ArgumentNullException(nameof(page));

            if (!(sheetWidth > 0.0) || !(sheetHeight > 0.0))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sheetWidth),
                    "A sheet has a width and a height above nought."
                );
            }

            if (copies < 1 || copies > 999)
            {
                throw new ArgumentOutOfRangeException(nameof(copies), copies, "Copies are 1 to 999.");
            }

            SheetWidth = sheetWidth;
            SheetHeight = sheetHeight;
            Copies = copies;
        }

        /// <summary>Gets the name the job goes by in the printer's queue: the drawing and the space, "plan-level2.dxf - Model".</summary>
        public string Title { get; }

        /// <summary>Gets the sheet of the printer's list the plot is printed on.</summary>
        public CadPaper Paper { get; }

        /// <summary>Gets the width of the sheet as it lies, in millimetres: 420 for a sheet of A3 on its side.</summary>
        public double SheetWidth { get; }

        /// <summary>Gets the height of the sheet as it lies, in millimetres: 297 for a sheet of A3 on its side.</summary>
        public double SheetHeight { get; }

        /// <summary>Gets the number of copies.</summary>
        public int Copies { get; }

        /// <summary>Gets the plot drawn on its sheet, owned by the thread that made it: print it on that thread.</summary>
        public DrawingVisual Page { get; }
    }

    /// <summary>
    /// A printer of Windows the Plot dialog lists beside the presenter's own plotters: its name, the sheets it lists and a way
    /// to print a page on it. The window gives <see cref="WindowsPlotPrinter"/>, which prints through WPF; a test gives a queue
    /// that prints nothing.
    /// </summary>
    public interface IPlotPrinter
    {
        /// <summary>Gets the name Windows gives the printer: <c>Microsoft Print to PDF</c>, or <c>\\print\HP LaserJet</c> for one on the network.</summary>
        string Name { get; }

        /// <summary>Gets whether the printer is on the network.</summary>
        bool IsNetwork { get; }

        /// <summary>
        /// Lists the sheets the printer prints on, as it names them, each with its size in millimetres, upright as printers name
        /// them, and the margins it cannot print in. Asked when the printer is chosen, not when the list is made.
        /// </summary>
        /// <returns>The sheets in the printer's order; empty where it lists none.</returns>
        IReadOnlyList<CadPaper> GetPapers();

        /// <summary>Prints a page, in the thread that owns it.</summary>
        /// <param name="job">The page, its paper and its copies.</param>
        /// <exception cref="InvalidOperationException">Thrown when the printer cannot take the job.</exception>
        void Print(PlotPrintJob job);
    }
}
