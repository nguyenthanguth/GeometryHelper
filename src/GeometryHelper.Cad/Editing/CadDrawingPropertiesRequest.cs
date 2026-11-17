using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using GeometryHelper.Dxf;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// The Drawing Properties dialog of DWGPROPS, as data its command and the window share. The Summary tab's texts and the
    /// custom properties are the drawing's <see cref="DxfDatabase.SummaryInfo"/> as the dialog has them, written back on OK as
    /// one undo step; the General tab (the file) and the Statistics tab (made, saved, edited) are shown as text and never
    /// written. A drawing of R2004 or later keeps the properties in its header, an older one in its DWGPROPS record, as
    /// AutoCAD saves them, and a DWG written by the presenter keeps them too.
    /// </summary>
    public sealed class CadDrawingPropertiesRequest
    {
        private readonly DxfDatabaseSummaryInfo _info;

        internal CadDrawingPropertiesRequest(CadDocument document)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            DxfDatabase database = document.Database;
            _info = database.SummaryInfo;
            Title = _info.Title;
            Subject = _info.Subject;
            Author = _info.Author;
            Keywords = _info.Keywords;
            Comments = _info.Comments;
            HyperlinkBase = _info.HyperlinkBase;
            CustomProperties = new List<KeyValuePair<string, string>>(_info.CustomProperties);
            FileName = document.Title;
            Statistics = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("Created", Date(database.Tdcreate)),
                new KeyValuePair<string, string>("Modified", Date(database.Tdupdate)),
                new KeyValuePair<string, string>("Last saved by", LastSavedBy(database, _info)),
                new KeyValuePair<string, string>("Revision number", _info.RevisionNumber),
                new KeyValuePair<string, string>("Total editing time", EditingTime(database.Tdindwg)),
            };
            General = FileRows(document);
            WindowTitle = FileName + " Properties";
        }

        /// <summary>Gets the title of the window: the name of the file and "Properties", as AutoCAD writes <c>Plan.dwg Properties</c>.</summary>
        public string WindowTitle { get; }

        /// <summary>Gets the name of the file without its folder, or "Drawing" where the drawing has none.</summary>
        public string FileName { get; }

        /// <summary>
        /// Gets the rows of the General tab: Type, Location, Size, Created, Modified and Accessed of the file, as the file system
        /// gives them; Type alone where the drawing has no file yet.
        /// </summary>
        public IReadOnlyList<KeyValuePair<string, string>> General { get; }

        /// <summary>Gets whether the file is read-only (the General tab's check box, greyed); false where there is no file.</summary>
        public bool IsReadOnly { get; private set; }

        /// <summary>Gets whether the file is hidden (the General tab's check box, greyed); false where there is no file.</summary>
        public bool IsHidden { get; private set; }

        /// <summary>
        /// Gets the rows of the Statistics tab: Created, Modified, Last saved by, Revision number and Total editing time, from the
        /// header (<c>$TDCREATE</c>, <c>$TDUPDATE</c>, <c>$TDINDWG</c>) and the properties; a date the header lacks is empty.
        /// </summary>
        public IReadOnlyList<KeyValuePair<string, string>> Statistics { get; }

        /// <summary>Gets or sets the title (Summary tab).</summary>
        public string Title { get; set; }

        /// <summary>Gets or sets the subject (Summary tab).</summary>
        public string Subject { get; set; }

        /// <summary>Gets or sets the author (Summary tab).</summary>
        public string Author { get; set; }

        /// <summary>Gets or sets the keywords (Summary tab).</summary>
        public string Keywords { get; set; }

        /// <summary>Gets or sets the comments (Summary tab); one line, as a group of DXF holds one.</summary>
        public string Comments { get; set; }

        /// <summary>Gets or sets the base of the drawing's relative hyperlinks (Summary tab).</summary>
        public string HyperlinkBase { get; set; }

        /// <summary>
        /// Gets the custom properties (Custom tab), each a name and a value in the order the drawing keeps them. Rows with no name
        /// and no value are left out on OK; a name two rows share, letters of either case the same, is refused.
        /// </summary>
        public List<KeyValuePair<string, string>> CustomProperties { get; }

        /// <summary>Gets or sets what the dialog was closed with; Cancel until set.</summary>
        public CadDialogAction Action { get; set; }

        /// <summary>
        /// Makes the properties the dialog stands for: the texts and custom properties as they are now, the drawing's last-saved-by
        /// and revision number kept as they were.
        /// </summary>
        /// <param name="info">The properties; null where they are refused.</param>
        /// <param name="problem">Why they are refused, as the dialog says it; null where they are not.</param>
        /// <returns>Whether the properties can be written.</returns>
        public bool TryBuild(out DxfDatabaseSummaryInfo info, out string problem)
        {
            try
            {
                var builder = new DxfDatabaseSummaryInfoBuilder(_info)
                {
                    Title = Title ?? string.Empty,
                    Subject = Subject ?? string.Empty,
                    Author = Author ?? string.Empty,
                    Keywords = Keywords ?? string.Empty,
                    Comments = Comments ?? string.Empty,
                    HyperlinkBase = HyperlinkBase ?? string.Empty,
                };
                builder.CustomPropertyTable.Clear();

                foreach (KeyValuePair<string, string> property in CustomProperties)
                {
                    string name = (property.Key ?? string.Empty).Trim();
                    string value = property.Value ?? string.Empty;

                    if (name.Length == 0 && value.Trim().Length == 0)
                    {
                        continue;
                    }

                    builder.CustomPropertyTable.Add(new KeyValuePair<string, string>(name, value));
                }

                info = builder.ToDatabaseSummaryInfo();
                problem = null;
                return true;
            }
            catch (ArgumentException exception)
            {
                info = null;
                problem = exception.Message;
                return false;
            }
        }

        /// <summary>
        /// Who saved the drawing last: the properties' own text where a DWGPROPS has one, else the header's <c>$LASTSAVEDBY</c>, which AutoCAD sets to the login name at each save (and ezdxf to its own name).
        /// </summary>
        private static string LastSavedBy(DxfDatabase database, DxfDatabaseSummaryInfo info)
        {
            if (info.LastSavedBy.Length > 0)
            {
                return info.LastSavedBy;
            }

            IReadOnlyList<DxfTypedValue> saved = database.GetHeader("$LASTSAVEDBY");
            return saved != null && saved.Count > 0 ? (saved[0].Value ?? string.Empty).Trim() : string.Empty;
        }

        /// <summary>A date as the Statistics tab writes it, in the words of the user's culture: Tuesday, November 17, 2026 3:32:48 PM; empty for none.</summary>
        internal static string Date(DateTime date) =>
            date == default(DateTime) ? string.Empty : date.ToString("F", CultureInfo.CurrentCulture);

        /// <summary>
        /// A time spent as AutoCAD writes it: "1 hour, 1 minute", "3 hours", "45 minutes", "0 minutes" for none; whole hours and
        /// minutes, the seconds left out.
        /// </summary>
        internal static string EditingTime(TimeSpan time)
        {
            int minutes = (int)Math.Min(int.MaxValue, Math.Floor(Math.Max(0.0, time.TotalMinutes)));
            int hours = minutes / 60;
            minutes %= 60;
            string h = hours.ToString(CultureInfo.InvariantCulture) + (hours == 1 ? " hour" : " hours");
            string m = minutes.ToString(CultureInfo.InvariantCulture) + (minutes == 1 ? " minute" : " minutes");
            return hours == 0 ? m
                : minutes == 0 ? h
                : h + ", " + m;
        }

        /// <summary>A size as AutoCAD writes it: 12.50KB (12 800 bytes), in megabytes from one.</summary>
        internal static string Size(long bytes)
        {
            string whole = bytes.ToString("N0", CultureInfo.CurrentCulture);
            return bytes >= 1024L * 1024L
                ? (bytes / 1048576.0).ToString("0.00", CultureInfo.CurrentCulture) + "MB (" + whole + " bytes)"
                : (bytes / 1024.0).ToString("0.00", CultureInfo.CurrentCulture) + "KB (" + whole + " bytes)";
        }

        private List<KeyValuePair<string, string>> FileRows(CadDocument document)
        {
            string name = document.FileName ?? string.Empty;
            string extension = Path.GetExtension(name).ToLowerInvariant();
            var rows = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>(
                    "Type",
                    extension == ".dxf" ? "AutoCAD DXF"
                        : extension == ".dwt" ? "AutoCAD Drawing Template"
                        : "AutoCAD Drawing"
                ),
            };

            try
            {
                if (name.Length > 0 && File.Exists(name))
                {
                    var file = new FileInfo(name);
                    rows.Add(new KeyValuePair<string, string>("Location", file.DirectoryName ?? string.Empty));
                    rows.Add(new KeyValuePair<string, string>("Size", Size(file.Length)));
                    rows.Add(new KeyValuePair<string, string>("Created", Date(file.CreationTime)));
                    rows.Add(new KeyValuePair<string, string>("Modified", Date(file.LastWriteTime)));
                    rows.Add(new KeyValuePair<string, string>("Accessed", Date(file.LastAccessTime)));
                    IsReadOnly = file.IsReadOnly;
                    IsHidden = (file.Attributes & FileAttributes.Hidden) != 0;
                }
            }
            catch (Exception exception)
                when (exception is IOException
                    || exception is UnauthorizedAccessException
                    || exception is ArgumentException
                    || exception is NotSupportedException
                )
            {
                GeometryHelperLog.Debug(
                    "CadDrawingPropertiesRequest: the file could not be read: " + exception.Message
                );
            }

            return rows;
        }
    }
}
