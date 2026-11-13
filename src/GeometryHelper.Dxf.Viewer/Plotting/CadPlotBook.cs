using System;
using System.Collections.Generic;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// Plots of several spaces written as one PDF of a page each, as EXPORTPDF and -EXPORT write All layouts: the window's
    /// writer is asked for it by the request -PLOT with this as its payload (<see cref="CadUiRequestEventArgs.Payload"/>). A
    /// drawing of Layout1 on ISO A3 and Layout2 on ISO A1 is a PDF of two pages, 420 by 297 mm and 841 by 594 mm.
    /// </summary>
    public sealed class CadPlotBook
    {
        /// <summary>Makes the plots to hand to the window.</summary>
        /// <param name="pages">The plot of each page, in the order of the pages.</param>
        /// <param name="fileName">The PDF file to write.</param>
        /// <exception cref="ArgumentNullException">Thrown when the pages or the file are null.</exception>
        /// <exception cref="ArgumentException">Thrown when there is no page.</exception>
        public CadPlotBook(IReadOnlyList<CadPlotJob> pages, string fileName)
        {
            Pages = pages ?? throw new ArgumentNullException(nameof(pages));
            FileName = fileName ?? throw new ArgumentNullException(nameof(fileName));

            if (pages.Count == 0)
            {
                throw new ArgumentException("A PDF of several plots has a page at least.", nameof(pages));
            }
        }

        /// <summary>Gets the plot of each page, in the order of the pages.</summary>
        public IReadOnlyList<CadPlotJob> Pages { get; }

        /// <summary>Gets the PDF file to write.</summary>
        public string FileName { get; }
    }
}
