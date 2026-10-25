using System;
using System.Collections.Generic;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// Makes the properties of a drawing, as AutoCAD's <c>DatabaseSummaryInfoBuilder</c>: each set as it is to be, then
    /// <see cref="ToDatabaseSummaryInfo"/> gives the <see cref="DxfDatabaseSummaryInfo"/> to set as
    /// <see cref="DxfDatabase.SummaryInfo"/>.
    /// </summary>
    public sealed class DxfDatabaseSummaryInfoBuilder
    {
        private string _title = string.Empty;
        private string _subject = string.Empty;
        private string _author = string.Empty;
        private string _keywords = string.Empty;
        private string _comments = string.Empty;
        private string _lastSavedBy = string.Empty;
        private string _revisionNumber = string.Empty;
        private string _hyperlinkBase = string.Empty;

        /// <summary>Initializes a builder of empty properties, as AutoCAD's <c>DatabaseSummaryInfoBuilder()</c>.</summary>
        public DxfDatabaseSummaryInfoBuilder()
        {
        }

        /// <summary>Initializes a builder of the properties given, as AutoCAD's <c>DatabaseSummaryInfoBuilder(info)</c>.</summary>
        /// <param name="info">The properties to start from.</param>
        /// <exception cref="ArgumentNullException">Thrown when the properties are null.</exception>
        public DxfDatabaseSummaryInfoBuilder(DxfDatabaseSummaryInfo info)
        {
            if (info == null)
            {
                throw new ArgumentNullException(nameof(info));
            }

            _title = info.Title;
            _subject = info.Subject;
            _author = info.Author;
            _comments = info.Comments;
            _keywords = info.Keywords;
            _lastSavedBy = info.LastSavedBy;
            _revisionNumber = info.RevisionNumber;
            _hyperlinkBase = info.HyperlinkBase;
            CustomPropertyTable.AddRange(info.CustomProperties);
        }

        /// <summary>Gets or sets the title, as AutoCAD's <c>Title</c>.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the text set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the text set has a line break in it.</exception>
        public string Title { get => _title; set => _title = DxfDatabaseSummaryInfo.Line(value, nameof(value)); }

        /// <summary>Gets or sets the subject, as AutoCAD's <c>Subject</c>.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the text set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the text set has a line break in it.</exception>
        public string Subject { get => _subject; set => _subject = DxfDatabaseSummaryInfo.Line(value, nameof(value)); }

        /// <summary>Gets or sets the author, as AutoCAD's <c>Author</c>.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the text set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the text set has a line break in it.</exception>
        public string Author { get => _author; set => _author = DxfDatabaseSummaryInfo.Line(value, nameof(value)); }

        /// <summary>Gets or sets the keywords, as AutoCAD's <c>Keywords</c>.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the text set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the text set has a line break in it.</exception>
        public string Keywords { get => _keywords; set => _keywords = DxfDatabaseSummaryInfo.Line(value, nameof(value)); }

        /// <summary>Gets or sets the comments, as AutoCAD's <c>Comments</c>; one line, as a group of DXF holds one.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the text set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the text set has a line break in it.</exception>
        public string Comments { get => _comments; set => _comments = DxfDatabaseSummaryInfo.Line(value, nameof(value)); }

        /// <summary>Gets or sets who saved the drawing last, as AutoCAD's <c>LastSavedBy</c>.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the text set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the text set has a line break in it.</exception>
        public string LastSavedBy { get => _lastSavedBy; set => _lastSavedBy = DxfDatabaseSummaryInfo.Line(value, nameof(value)); }

        /// <summary>Gets or sets the revision, as AutoCAD's <c>RevisionNumber</c>.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the text set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the text set has a line break in it.</exception>
        public string RevisionNumber { get => _revisionNumber; set => _revisionNumber = DxfDatabaseSummaryInfo.Line(value, nameof(value)); }

        /// <summary>Gets or sets the base of relative hyperlinks, as AutoCAD's <c>HyperlinkBase</c>.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the text set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the text set has a line break in it.</exception>
        public string HyperlinkBase { get => _hyperlinkBase; set => _hyperlinkBase = DxfDatabaseSummaryInfo.Line(value, nameof(value)); }

        /// <summary>
        /// Gets the custom properties, as AutoCAD's <c>CustomPropertyTable</c>: each a name and a value, in the order they are
        /// written; a list rather than AutoCAD's dictionary, so that a drawing keeps the order it has. Names are told apart with
        /// letters of either case the same, as AutoCAD tells them.
        /// </summary>
        public List<KeyValuePair<string, string>> CustomPropertyTable { get; } = new List<KeyValuePair<string, string>>();

        /// <summary>Gives the properties built, as AutoCAD's <c>ToDatabaseSummaryInfo</c>.</summary>
        /// <returns>The properties.</returns>
        /// <exception cref="ArgumentException">Thrown when a custom property has no name, a name or value that is null or has a
        /// line break in it, or a name another has, letters of either case the same.</exception>
        public DxfDatabaseSummaryInfo ToDatabaseSummaryInfo()
        {
            var custom = new List<KeyValuePair<string, string>>(CustomPropertyTable.Count);

            foreach (KeyValuePair<string, string> property in CustomPropertyTable)
            {
                if (property.Key == null || property.Key.Trim().Length == 0 || property.Value == null)
                {
                    throw new ArgumentException("A custom property has a name and a value.", nameof(CustomPropertyTable));
                }

                DxfDatabaseSummaryInfo.Line(property.Key, nameof(CustomPropertyTable));
                DxfDatabaseSummaryInfo.Line(property.Value, nameof(CustomPropertyTable));

                if (custom.Exists(p => string.Equals(p.Key, property.Key, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new ArgumentException("Two custom properties are named " + property.Key + ", letters of either case the same.", nameof(CustomPropertyTable));
                }

                custom.Add(property);
            }

            return new DxfDatabaseSummaryInfo(new[] { _title, _subject, _author, _comments, _keywords, _lastSavedBy, _revisionNumber }, _hyperlinkBase, custom);
        }
    }
}
