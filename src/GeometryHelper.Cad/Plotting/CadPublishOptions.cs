using System;

namespace GeometryHelper.Cad
{
    /// <summary>Where the sheets of a publish go, as the Publish to list of AutoCAD 2021's Publish dialog: its Plotter named in page setup, or a PDF. DWF and DWFx have no writer here.</summary>
    public enum CadPublishTo
    {
        /// <summary>Each sheet goes to the plotter its page setup names, where this computer has that printer; a sheet whose plotter is not a printer here is written as a PDF of its own.</summary>
        PlotterNamedInPageSetup,

        /// <summary>The sheets are written as PDF: one file of a page each, or a file for each sheet (<see cref="CadPublishOptions.MultiSheet"/>).</summary>
        Pdf,
    }

    /// <summary>
    /// What the Publish dialog sets besides the list of sheets: where they go, one PDF or a PDF each, the number of copies,
    /// the folder and the name of the PDF, which sheets are listed when a drawing is added, and what is done once it is
    /// published. A sheet list file (.dsd) keeps the first five and Open in viewer (see <see cref="CadDsdFile"/>); Publish in
    /// background is a box that does nothing here, as every publish runs at once.
    /// </summary>
    public sealed class CadPublishOptions
    {
        private int _copies = 1;

        /// <summary>Gets or sets where the sheets go; <see cref="CadPublishTo.Pdf"/> until set.</summary>
        public CadPublishTo PublishTo { get; set; } = CadPublishTo.Pdf;

        /// <summary>
        /// Gets or sets whether a PDF is one file of a page for each sheet (the multi-sheet file of AutoCAD) rather than a file
        /// for each sheet; true until set. Sheets plotted as the plotter of their page setup is a printer are not in the file.
        /// </summary>
        public bool MultiSheet { get; set; } = true;

        /// <summary>Gets or sets the number of copies, 1 to 99, printed of each sheet that goes to a printer; a PDF has one.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number is not 1 to 99, as AutoCAD refuses it.</exception>
        public int Copies
        {
            get => _copies;
            set
            {
                if (value < 1 || value > 99)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(value),
                        value,
                        "The number of copies must be between 1 and 99."
                    );
                }

                _copies = value;
            }
        }

        /// <summary>
        /// Gets or sets the folder the PDFs are written to, AutoCAD's Default output location; empty for the folder of the
        /// first drawing listed that has a file, else Documents.
        /// </summary>
        public string OutputFolder { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the name of the multi-sheet PDF, with or without .pdf, and with a folder where it is not the output
        /// folder; empty for the name of the first drawing listed, as <c>plan-level2.pdf</c> for plan-level2.dxf.
        /// </summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>Gets or sets AutoCAD's Include Model when adding sheets: true until set.</summary>
        public bool IncludeModel { get; set; } = true;

        /// <summary>Gets or sets AutoCAD's Include Layouts when adding sheets: true until set.</summary>
        public bool IncludeLayouts { get; set; } = true;

        /// <summary>Gets or sets AutoCAD's Publish in background. The box is kept and does nothing: a publish here runs at once and ends with its report.</summary>
        public bool PublishInBackground { get; set; }

        /// <summary>Gets or sets AutoCAD's Open in viewer when done: each PDF written is opened in the program Windows gives its type.</summary>
        public bool OpenInViewer { get; set; }

        /// <summary>Makes a copy that changes apart from this one.</summary>
        /// <returns>The copy.</returns>
        public CadPublishOptions Clone() => (CadPublishOptions)MemberwiseClone();
    }
}
