using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>What an Insert Hyperlink dialog links to, as its "Link to" column offers.</summary>
    public enum CadHyperlinkKind
    {
        /// <summary>Existing File or Web Page: an address, and a named place in it.</summary>
        FileOrWebPage,

        /// <summary>View of This Drawing: a named view of the drawing the object is in.</summary>
        ViewOfThisDrawing,

        /// <summary>E-mail Address: an address and a subject, written as <c>mailto:</c>.</summary>
        EmailAddress,
    }

    /// <summary>
    /// The Insert Hyperlink and Edit Hyperlink dialog of HYPERLINK, as data its command and the window share: what the link goes
    /// to and the text it is shown by. Where an object selected has a hyperlink the dialog is Edit Hyperlink and starts from the
    /// first one found; OK then makes that the only hyperlink of every object selected, and Remove Link takes every hyperlink
    /// from them. Where none has one, OK adds it.
    /// </summary>
    public sealed class CadHyperlinkRequest
    {
        private const string MailTo = "mailto:";
        private const string SubjectMark = "?subject=";

        internal CadHyperlinkRequest(CadDocument document, IReadOnlyList<string> handles)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            ObjectCount = handles?.Count ?? 0;
            var views = new List<string>();

            foreach (DxfViewTableRecord view in document.Database.ViewTable)
            {
                views.Add(view.Name);
            }

            views.Sort(
                (a, b) =>
                {
                    int order = StringComparer.OrdinalIgnoreCase.Compare(a, b);
                    return order != 0 ? order : string.CompareOrdinal(a, b);
                }
            );
            Views = views;
            List<DxfEntity> linked = CadHyperlinks.WithLinks(
                document,
                handles ?? (IReadOnlyList<string>)Array.Empty<string>()
            );
            IsEdit = linked.Count > 0;

            if (IsEdit)
            {
                DxfHyperLink first = linked[0].Hyperlinks[0];
                TextToDisplay = first.Description;
                string name = first.Name;

                if (name.StartsWith(MailTo, StringComparison.OrdinalIgnoreCase))
                {
                    Kind = CadHyperlinkKind.EmailAddress;
                    int subject = name.IndexOf(SubjectMark, StringComparison.OrdinalIgnoreCase);
                    EmailAddress = (subject < 0 ? name : name.Substring(0, subject)).Substring(MailTo.Length);
                    Subject = subject < 0 ? string.Empty : name.Substring(subject + SubjectMark.Length);
                }
                else if (name.Length == 0 && first.SubLocation.Length > 0)
                {
                    Kind = CadHyperlinkKind.ViewOfThisDrawing;
                    ViewName = first.SubLocation;
                }
                else
                {
                    Address = name;
                    Location = first.SubLocation;
                }
            }
        }

        /// <summary>Gets how many objects the hyperlink is for.</summary>
        public int ObjectCount { get; }

        /// <summary>Gets whether an object selected has a hyperlink: the dialog is Edit Hyperlink, with Remove Link, rather than Insert Hyperlink.</summary>
        public bool IsEdit { get; }

        /// <summary>Gets the title of the dialog: "Edit Hyperlink" or "Insert Hyperlink".</summary>
        public string Title => IsEdit ? "Edit Hyperlink" : "Insert Hyperlink";

        /// <summary>Gets the names of the views of the drawing, in ordinal order, for View of This Drawing.</summary>
        public IReadOnlyList<string> Views { get; }

        /// <summary>Gets or sets what the link goes to; an existing file or web page until set.</summary>
        public CadHyperlinkKind Kind { get; set; }

        /// <summary>Gets or sets the text to display, which the object's tooltip shows; empty to show the address.</summary>
        public string TextToDisplay { get; set; } = string.Empty;

        /// <summary>Gets or sets the file or web page, <c>plans\B-12.pdf</c> or <c>https://example.com</c> (Existing File or Web Page).</summary>
        public string Address { get; set; } = string.Empty;

        /// <summary>Gets or sets the named place in the file or page (the dialog's Target); empty for none.</summary>
        public string Location { get; set; } = string.Empty;

        /// <summary>Gets or sets the view of the drawing (View of This Drawing).</summary>
        public string ViewName { get; set; } = string.Empty;

        /// <summary>Gets or sets the e-mail address (E-mail Address).</summary>
        public string EmailAddress { get; set; } = string.Empty;

        /// <summary>Gets or sets the subject of the mail (E-mail Address); empty for none.</summary>
        public string Subject { get; set; } = string.Empty;

        /// <summary>Gets or sets whether Remove Link was pressed: every hyperlink of the objects is taken out and nothing is written.</summary>
        public bool Remove { get; set; }

        /// <summary>Gets or sets what the dialog was closed with; Cancel until set.</summary>
        public CadDialogAction Action { get; set; }

        /// <summary>
        /// Makes the hyperlink the dialog stands for. A file or web page needs an address, a view a view of the drawing, a mail an
        /// address; an address, text or place longer than 255 characters is refused with AutoCAD's words.
        /// </summary>
        /// <param name="link">The hyperlink; null where it is refused.</param>
        /// <param name="problem">Why it is refused, as the dialog says it; null where it is not.</param>
        /// <returns>Whether the hyperlink can be written.</returns>
        public bool TryBuild(out DxfHyperLink link, out string problem)
        {
            link = null;
            problem = null;
            string address;
            string place = string.Empty;

            switch (Kind)
            {
                case CadHyperlinkKind.ViewOfThisDrawing:
                    if (string.IsNullOrWhiteSpace(ViewName))
                    {
                        problem = "Choose a view of this drawing.";
                        return false;
                    }

                    address = string.Empty;
                    place = ViewName.Trim();
                    break;
                case CadHyperlinkKind.EmailAddress:
                    if (string.IsNullOrWhiteSpace(EmailAddress))
                    {
                        problem = "Type an e-mail address.";
                        return false;
                    }

                    address =
                        MailTo
                        + EmailAddress.Trim()
                        + (string.IsNullOrWhiteSpace(Subject) ? string.Empty : SubjectMark + Subject.Trim());
                    break;
                default:
                    if (string.IsNullOrWhiteSpace(Address))
                    {
                        problem = "Type the file or web page name.";
                        return false;
                    }

                    address = Address;
                    place = Location ?? string.Empty;
                    break;
            }

            DxfHyperLink made = CadHyperlinks.Make(address, place, TextToDisplay);
            problem = CadHyperlinks.TooLong(made);

            if (problem != null)
            {
                return false;
            }

            link = made;
            return true;
        }

        /// <inheritdoc/>
        public override string ToString() =>
            Title + " (" + ObjectCount.ToString(CultureInfo.InvariantCulture) + " objects)";
    }
}
