namespace GeometryHelper.Drawing
{
    /// <summary>
    /// The texts of a drawing's title block, written as the attributes of the block GH_TITLE in the lower right corner of
    /// the frame, 180 by 40 on paper.
    /// </summary>
    /// <remarks>
    /// Each text left empty is filled when the drawing is made where the source has it: the title from the main part, as
    /// "BEAM B1"; the drawing number from the drawing's id. A null set is taken as empty.
    /// </remarks>
    public sealed class TitleBlock
    {
        private string _project = string.Empty;
        private string _drawingNumber = string.Empty;
        private string _title = string.Empty;
        private string _revision = string.Empty;
        private string _company = string.Empty;
        private string _drawnBy = string.Empty;
        private string _date = string.Empty;

        /// <summary>The width of the title block on paper, in millimetres.</summary>
        public const double Width = 180.0;

        /// <summary>The height of the title block on paper, in millimetres.</summary>
        public const double Height = 40.0;

        /// <summary>Gets or sets the project's name.</summary>
        public string Project
        {
            get => _project;
            set => _project = value ?? string.Empty;
        }

        /// <summary>Gets or sets the drawing number; the drawing's id where it is left empty.</summary>
        public string DrawingNumber
        {
            get => _drawingNumber;
            set => _drawingNumber = value ?? string.Empty;
        }

        /// <summary>Gets or sets the title; the main part's name and mark where it is left empty.</summary>
        public string Title
        {
            get => _title;
            set => _title = value ?? string.Empty;
        }

        /// <summary>Gets or sets the revision.</summary>
        public string Revision
        {
            get => _revision;
            set => _revision = value ?? string.Empty;
        }

        /// <summary>Gets or sets the company's name.</summary>
        public string Company
        {
            get => _company;
            set => _company = value ?? string.Empty;
        }

        /// <summary>Gets or sets who drew it.</summary>
        public string DrawnBy
        {
            get => _drawnBy;
            set => _drawnBy = value ?? string.Empty;
        }

        /// <summary>Gets or sets the date, as it is to be written; nothing fills it in, so the same drawing is the same file.</summary>
        public string Date
        {
            get => _date;
            set => _date = value ?? string.Empty;
        }

        internal TitleBlock Clone() => (TitleBlock)MemberwiseClone();
    }
}
