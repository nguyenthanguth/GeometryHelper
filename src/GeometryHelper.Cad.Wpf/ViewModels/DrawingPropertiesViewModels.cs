using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using GeometryHelper.Dxf;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>One row of the Custom tab of Drawing Properties: a name and its value, both edited in place.</summary>
    public sealed class CustomPropertyRowViewModel : ObservableObject
    {
        private string _name;
        private string _value;

        /// <summary>Makes a row.</summary>
        /// <param name="name">The name of the property; empty for a row just added.</param>
        /// <param name="value">Its value.</param>
        public CustomPropertyRowViewModel(string name, string value)
        {
            _name = name ?? string.Empty;
            _value = value ?? string.Empty;
        }

        /// <summary>Gets or sets the name, <c>Part number</c>; letters of either case are the same name.</summary>
        public string Name
        {
            get => _name;
            set => Set(ref _name, value ?? string.Empty);
        }

        /// <summary>Gets or sets the value, <c>B-12</c>.</summary>
        public string Value
        {
            get => _value;
            set => Set(ref _value, value ?? string.Empty);
        }
    }

    /// <summary>
    /// AutoCAD's Drawing Properties dialog, which DWGPROPS shows, over the request its command hands the window: the General
    /// tab (the file, read only), Summary (Title, Subject, Author, Keywords, Comments, Hyperlink base), Statistics (Created,
    /// Modified, Last saved by, Revision number, Total editing time, read only) and Custom (name and value rows with Add and
    /// Delete). OK writes the Summary and Custom tabs to the drawing as one undo step; a custom property with no name or a
    /// name used twice is refused in the dialog's footer and the dialog stays open.
    /// </summary>
    public sealed class DrawingPropertiesViewModel : ObservableObject
    {
        private string _tab = "General";
        private string _notice = string.Empty;

        /// <summary>Makes the dialog over a request.</summary>
        /// <param name="request">The request of DWGPROPS.</param>
        /// <exception cref="ArgumentNullException">Thrown when the request is null.</exception>
        public DrawingPropertiesViewModel(CadDrawingPropertiesRequest request)
        {
            Request = request ?? throw new ArgumentNullException(nameof(request));
            CustomProperties = new ObservableCollection<CustomPropertyRowViewModel>(
                request.CustomProperties.Select(p => new CustomPropertyRowViewModel(p.Key, p.Value))
            );
            AddCommand = new DelegateCommand(_ => Add());
            DeleteCommand = new DelegateCommand(row => Delete(row as CustomPropertyRowViewModel));
        }

        /// <summary>Gets the request.</summary>
        public CadDrawingPropertiesRequest Request { get; }

        /// <summary>Gets the title of the window: <c>Plan.dwg Properties</c>.</summary>
        public string WindowTitle => Request.WindowTitle;

        /// <summary>Gets the name of the file, the General tab's heading.</summary>
        public string FileName => Request.FileName;

        /// <summary>Gets the rows of the General tab, each a label and its text.</summary>
        public IReadOnlyList<KeyValuePair<string, string>> General => Request.General;

        /// <summary>Gets whether the file is read only (the General tab's greyed box).</summary>
        public bool IsReadOnly => Request.IsReadOnly;

        /// <summary>Gets whether the file is hidden (the General tab's greyed box).</summary>
        public bool IsHidden => Request.IsHidden;

        /// <summary>Gets the rows of the Statistics tab, each a label and its text.</summary>
        public IReadOnlyList<KeyValuePair<string, string>> Statistics => Request.Statistics;

        /// <summary>Gets or sets which tab is shown: General, Summary, Statistics or Custom.</summary>
        public string Tab
        {
            get => _tab;
            set
            {
                if (Set(ref _tab, value))
                {
                    Raise(nameof(IsGeneralTab));
                    Raise(nameof(IsSummaryTab));
                    Raise(nameof(IsStatisticsTab));
                    Raise(nameof(IsCustomTab));
                }
            }
        }

        /// <summary>Gets or sets whether the General tab is shown.</summary>
        public bool IsGeneralTab
        {
            get => _tab == "General";
            set => ShowTab(value, "General");
        }

        /// <summary>Gets or sets whether the Summary tab is shown.</summary>
        public bool IsSummaryTab
        {
            get => _tab == "Summary";
            set => ShowTab(value, "Summary");
        }

        /// <summary>Gets or sets whether the Statistics tab is shown.</summary>
        public bool IsStatisticsTab
        {
            get => _tab == "Statistics";
            set => ShowTab(value, "Statistics");
        }

        /// <summary>Gets or sets whether the Custom tab is shown.</summary>
        public bool IsCustomTab
        {
            get => _tab == "Custom";
            set => ShowTab(value, "Custom");
        }

        /// <summary>Gets or sets the title.</summary>
        public string Title
        {
            get => Request.Title;
            set => Edit(() => Request.Title = value, nameof(Title));
        }

        /// <summary>Gets or sets the subject.</summary>
        public string Subject
        {
            get => Request.Subject;
            set => Edit(() => Request.Subject = value, nameof(Subject));
        }

        /// <summary>Gets or sets the author.</summary>
        public string Author
        {
            get => Request.Author;
            set => Edit(() => Request.Author = value, nameof(Author));
        }

        /// <summary>Gets or sets the keywords.</summary>
        public string Keywords
        {
            get => Request.Keywords;
            set => Edit(() => Request.Keywords = value, nameof(Keywords));
        }

        /// <summary>Gets or sets the comments.</summary>
        public string Comments
        {
            get => Request.Comments;
            set => Edit(() => Request.Comments = value, nameof(Comments));
        }

        /// <summary>Gets or sets the base of the drawing's relative hyperlinks.</summary>
        public string HyperlinkBase
        {
            get => Request.HyperlinkBase;
            set => Edit(() => Request.HyperlinkBase = value, nameof(HyperlinkBase));
        }

        /// <summary>Gets the custom properties, edited in place.</summary>
        public ObservableCollection<CustomPropertyRowViewModel> CustomProperties { get; }

        /// <summary>Gets the command of the Add button: a row with no name or value, at the end.</summary>
        public ICommand AddCommand { get; }

        /// <summary>Gets the command that takes a row out; its parameter is the row.</summary>
        public ICommand DeleteCommand { get; }

        /// <summary>Gets why OK was refused, in the footer; empty for nothing.</summary>
        public string Notice
        {
            get => _notice;
            private set => Set(ref _notice, value);
        }

        /// <summary>Adds an empty row at the end of the custom properties and shows the Custom tab.</summary>
        public void Add()
        {
            CustomProperties.Add(new CustomPropertyRowViewModel(string.Empty, string.Empty));
            Tab = "Custom";
        }

        /// <summary>Takes a row out of the custom properties.</summary>
        /// <param name="row">The row; nothing happens for null or one not in the list.</param>
        public void Delete(CustomPropertyRowViewModel row)
        {
            if (row != null)
            {
                CustomProperties.Remove(row);
            }
        }

        /// <summary>
        /// OK: the custom rows given to the request, which refuses what cannot be written (a property with no name, a name used
        /// twice); then the request is answered OK and the dialog may close.
        /// </summary>
        /// <returns>False where the properties were refused, the reason in <see cref="Notice"/>; true where the dialog may close.</returns>
        public bool Accept()
        {
            Request.CustomProperties.Clear();
            Request.CustomProperties.AddRange(
                CustomProperties.Select(r => new KeyValuePair<string, string>(r.Name, r.Value))
            );

            if (!Request.TryBuild(out _, out string problem))
            {
                Notice = problem;
                return false;
            }

            Notice = string.Empty;
            Request.Action = CadDialogAction.Ok;
            return true;
        }

        /// <summary>Cancel: the request is answered Cancel and nothing is written.</summary>
        public void Cancel() => Request.Action = CadDialogAction.Cancel;

        private void ShowTab(bool shown, string tab)
        {
            if (shown)
            {
                Tab = tab;
            }
        }

        private void Edit(Action set, string property)
        {
            set();
            Raise(property);
        }
    }
}
