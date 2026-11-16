using System;
using System.Collections.Generic;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// AutoCAD's Insert Hyperlink dialog, which HYPERLINK shows (Edit Hyperlink, with Remove Link, where an object already has a
    /// link), over the request its command hands the window. "Link to" offers Existing File or Web Page (the address and its
    /// named location, Browse… for a file), View of This Drawing (a view of the drawing, chosen from a list) and E-mail Address
    /// (the address and a subject); "Text to display" is what the object's tooltip shows. OK is refused in the footer for what
    /// AutoCAD refuses: no address, no view, a text longer than 255 characters.
    /// </summary>
    public sealed class HyperlinkViewModel : ObservableObject
    {
        private string _notice = string.Empty;

        /// <summary>Makes the dialog over a request.</summary>
        /// <param name="request">The request of HYPERLINK.</param>
        /// <exception cref="ArgumentNullException">Thrown when the request is null.</exception>
        public HyperlinkViewModel(CadHyperlinkRequest request)
        {
            Request = request ?? throw new ArgumentNullException(nameof(request));
        }

        /// <summary>Gets the request.</summary>
        public CadHyperlinkRequest Request { get; }

        /// <summary>Gets or sets how the window browses for a file: the path chosen, or null where it was cancelled. Null for no browsing.</summary>
        public Func<string> BrowseFile { get; set; }

        /// <summary>Gets the title of the window: Insert Hyperlink, or Edit Hyperlink where an object has a link.</summary>
        public string WindowTitle => Request.Title;

        /// <summary>Gets whether Remove Link is offered: the objects have a hyperlink.</summary>
        public bool CanRemove => Request.IsEdit;

        /// <summary>Gets the views of the drawing, for View of This Drawing.</summary>
        public IReadOnlyList<string> Views => Request.Views;

        /// <summary>Gets or sets whether the link goes to an existing file or web page.</summary>
        public bool IsFileKind
        {
            get => Request.Kind == CadHyperlinkKind.FileOrWebPage;
            set => Choose(value, CadHyperlinkKind.FileOrWebPage);
        }

        /// <summary>Gets or sets whether the link goes to a view of this drawing.</summary>
        public bool IsViewKind
        {
            get => Request.Kind == CadHyperlinkKind.ViewOfThisDrawing;
            set => Choose(value, CadHyperlinkKind.ViewOfThisDrawing);
        }

        /// <summary>Gets or sets whether the link goes to an e-mail address.</summary>
        public bool IsEmailKind
        {
            get => Request.Kind == CadHyperlinkKind.EmailAddress;
            set => Choose(value, CadHyperlinkKind.EmailAddress);
        }

        /// <summary>Gets or sets the text to display.</summary>
        public string TextToDisplay
        {
            get => Request.TextToDisplay;
            set => Edit(() => Request.TextToDisplay = value, nameof(TextToDisplay));
        }

        /// <summary>Gets or sets the file or web page.</summary>
        public string Address
        {
            get => Request.Address;
            set => Edit(() => Request.Address = value, nameof(Address));
        }

        /// <summary>Gets or sets the named place in the file or page.</summary>
        public string Location
        {
            get => Request.Location;
            set => Edit(() => Request.Location = value, nameof(Location));
        }

        /// <summary>Gets or sets the view of the drawing chosen.</summary>
        public string ViewName
        {
            get => Request.ViewName;
            set => Edit(() => Request.ViewName = value, nameof(ViewName));
        }

        /// <summary>Gets or sets the e-mail address.</summary>
        public string EmailAddress
        {
            get => Request.EmailAddress;
            set => Edit(() => Request.EmailAddress = value, nameof(EmailAddress));
        }

        /// <summary>Gets or sets the subject of the mail.</summary>
        public string Subject
        {
            get => Request.Subject;
            set => Edit(() => Request.Subject = value, nameof(Subject));
        }

        /// <summary>Gets why OK was refused, in the footer; empty for nothing.</summary>
        public string Notice
        {
            get => _notice;
            private set => Set(ref _notice, value);
        }

        /// <summary>Browse…: the file chosen becomes the address.</summary>
        /// <returns>Whether a file was chosen.</returns>
        public bool Browse()
        {
            string path = BrowseFile?.Invoke();

            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            Address = path;
            return true;
        }

        /// <summary>OK: the hyperlink checked; the request is answered OK and the dialog may close where it can be written.</summary>
        /// <returns>False where it was refused, the reason in <see cref="Notice"/>; true where the dialog may close.</returns>
        public bool Accept()
        {
            if (!Request.TryBuild(out _, out string problem))
            {
                Notice = problem;
                return false;
            }

            Notice = string.Empty;
            Request.Action = CadDialogAction.Ok;
            return true;
        }

        /// <summary>Remove Link: the request answered OK with the hyperlinks of the objects to be taken out.</summary>
        public void RemoveLink()
        {
            Request.Remove = true;
            Request.Action = CadDialogAction.Ok;
        }

        /// <summary>Cancel: the request is answered Cancel and nothing is written.</summary>
        public void Cancel() => Request.Action = CadDialogAction.Cancel;

        private void Choose(bool chosen, CadHyperlinkKind kind)
        {
            if (!chosen || Request.Kind == kind)
            {
                return;
            }

            Request.Kind = kind;
            Notice = string.Empty;
            Raise(nameof(IsFileKind));
            Raise(nameof(IsViewKind));
            Raise(nameof(IsEmailKind));
        }

        private void Edit(Action set, string property)
        {
            set();
            Raise(property);
        }
    }
}
