using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The name of a new page setup, asked by Add… of the Plot dialog (Add Page Setup) and by New… of the Page Setup Manager
    /// (New Page Setup, which also asks what the new one starts with). A name AutoCAD refuses, or one the drawing has already, is
    /// shown with its reason and greys OK.
    /// </summary>
    public sealed class PageSetupNameViewModel : ObservableObject
    {
        private readonly List<string> _taken;
        private string _name;
        private string _startWith;

        /// <summary>Makes the dialog, on the first name <c>Setup1</c>, <c>Setup2</c> and on that the drawing has not.</summary>
        /// <param name="title">The title: "Add Page Setup" or "New Page Setup".</param>
        /// <param name="taken">The names the drawing has already.</param>
        /// <param name="startWith">What the new page setup can start with, as the manager lists it; null where it is not asked (Add…).</param>
        /// <exception cref="ArgumentNullException">Thrown when the title or the names taken are null.</exception>
        public PageSetupNameViewModel(string title, IEnumerable<string> taken, IReadOnlyList<string> startWith = null)
        {
            Title = title ?? throw new ArgumentNullException(nameof(title));
            _taken = new List<string>(taken ?? throw new ArgumentNullException(nameof(taken)));
            StartWith = startWith;
            _startWith = startWith != null && startWith.Count > 0 ? startWith[0] : null;

            int n = 1;

            while (IsTaken("Setup" + n.ToString(CultureInfo.InvariantCulture)))
            {
                n++;
            }

            _name = "Setup" + n.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Gets the title: "Add Page Setup" or "New Page Setup".</summary>
        public string Title { get; }

        /// <summary>Gets or sets the name as typed.</summary>
        public string Name
        {
            get => _name;
            set
            {
                if (Set(ref _name, value ?? string.Empty))
                {
                    Raise(nameof(Error));
                    Raise(nameof(HasError));
                    Raise(nameof(CanOk));
                }
            }
        }

        /// <summary>Gets why the name cannot be used, in a sentence; null where it can.</summary>
        public string Error =>
            CadPlotSettings.CheckPageSetupName(_name)
            ?? (IsTaken(_name.Trim()) ? "The drawing has a page setup named " + _name.Trim() + " already." : null);

        /// <summary>Gets whether the name has a fault to show.</summary>
        public bool HasError => Error != null;

        /// <summary>Gets whether OK is offered: the name is a good one.</summary>
        public bool CanOk => Error == null;

        /// <summary>Gets what the new page setup can start with, as the manager lists it; null where it is not asked.</summary>
        public IReadOnlyList<string> StartWith { get; }

        /// <summary>Gets whether the list of what to start with is shown.</summary>
        public bool HasStartWith => StartWith != null;

        /// <summary>Gets or sets what the new page setup starts with, one of <see cref="StartWith"/>.</summary>
        public string StartWithChoice
        {
            get => _startWith;
            set
            {
                if (value != null && StartWith != null && StartWith.Contains(value))
                {
                    Set(ref _startWith, value);
                }
            }
        }

        /// <summary>Gets the page of the guide the Help button opens.</summary>
        public string HelpUrl => PlotHelp.PageSetups;

        /// <summary>
        /// Gets or sets how Import… copies page setups of another drawing in, as Add Page Setup offers it beside OK: it returns the
        /// names the drawing has afterwards, which the name is then refused against, or null where nothing was imported. Null for
        /// none, when Import… is not offered.
        /// </summary>
        public Func<IEnumerable<string>> ImportPageSetups { get; set; }

        /// <summary>Gets whether Import… is offered.</summary>
        public bool CanImport => ImportPageSetups != null;

        /// <summary>
        /// Runs Import… : the page setups copied in are names the drawing has, so a name typed that is one of them is refused
        /// from then on.
        /// </summary>
        public void Import()
        {
            IEnumerable<string> names = ImportPageSetups?.Invoke();

            if (names == null)
            {
                return;
            }

            _taken.Clear();
            _taken.AddRange(names);
            Raise(nameof(Error));
            Raise(nameof(HasError));
            Raise(nameof(CanOk));
        }

        private bool IsTaken(string name) =>
            _taken.Exists(t => string.Equals(t, name, StringComparison.OrdinalIgnoreCase));
    }
}
