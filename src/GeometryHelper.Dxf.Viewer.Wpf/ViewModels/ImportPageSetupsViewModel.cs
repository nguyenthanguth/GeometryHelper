using System;
using System.Collections.Generic;
using System.Linq;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>A page setup of another drawing in the Import Page Setups list, ticked to be copied.</summary>
    public sealed class ImportPageSetupRow : ObservableObject
    {
        private bool _isChecked;

        internal ImportPageSetupRow(string name, bool isChecked, string location = "", PageSetupInfo info = null)
        {
            Name = name;
            _isChecked = isChecked;
            Location = location ?? string.Empty;
            Info = info ?? PageSetupInfo.Empty;
        }

        /// <summary>Gets the name of the page setup in the other drawing.</summary>
        public string Name { get; }

        /// <summary>Gets the space it was made for: Model or Layout; empty where it is not known.</summary>
        public string Location { get; }

        /// <summary>Gets how it plots, as the details under the list show it.</summary>
        public PageSetupInfo Info { get; }

        /// <summary>Gets or sets whether it is copied.</summary>
        public bool IsChecked
        {
            get => _isChecked;
            set => Set(ref _isChecked, value);
        }

        /// <inheritdoc/>
        public override string ToString() => Name;
    }

    /// <summary>
    /// Import Page Setups, from Import… of the Page Setup Manager and of Add Page Setup: the drawing the page setups come from,
    /// each of its page setups ticked to be copied into this one with the Model or Layout it was made for, and the details of the
    /// one chosen. All are ticked to begin with; OK is offered while one is.
    /// </summary>
    public sealed class ImportPageSetupsViewModel : ObservableObject
    {
        private ImportPageSetupRow _selected;

        /// <summary>Makes the dialog on the page setups of a drawing.</summary>
        /// <param name="fileName">The drawing, as its name is shown over the list.</param>
        /// <param name="names">The page setups it has, by name.</param>
        /// <exception cref="ArgumentNullException">Thrown when the file name or the names are null.</exception>
        public ImportPageSetupsViewModel(string fileName, IEnumerable<string> names)
            : this(
                fileName,
                (names ?? throw new ArgumentNullException(nameof(names))).Select(n => new ImportPageSetupRow(n, true))
            ) { }

        internal ImportPageSetupsViewModel(string fileName, IEnumerable<ImportPageSetupRow> rows)
        {
            FileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
            Items = (rows ?? throw new ArgumentNullException(nameof(rows))).ToList();

            foreach (ImportPageSetupRow row in Items)
            {
                row.PropertyChanged += (s, e) =>
                {
                    Raise(nameof(CanOk));
                    Raise(nameof(Chosen));
                };
            }
        }

        /// <summary>Gets the drawing the page setups come from, with its path as it was given.</summary>
        public string FileName { get; }

        /// <summary>Gets the title.</summary>
        public string Title => "Import Page Setups";

        /// <summary>Gets the words over the drawing's path: "Source drawing name:".</summary>
        public string FromText => "Source drawing name:";

        /// <summary>Gets the page of the guide the Help button opens.</summary>
        public string HelpUrl => PlotHelp.PageSetups;

        /// <summary>Gets the page setups of the drawing, each with its box.</summary>
        public IReadOnlyList<ImportPageSetupRow> Items { get; }

        /// <summary>Gets or sets the page setup whose details are shown; null for none.</summary>
        public ImportPageSetupRow Selected
        {
            get => _selected;
            set
            {
                if (Set(ref _selected, value))
                {
                    Raise(nameof(SelectedInfo));
                }
            }
        }

        /// <summary>Gets how the page setup chosen plots, in the details under the list; empty fields for none.</summary>
        public PageSetupInfo SelectedInfo => _selected?.Info ?? PageSetupInfo.Empty;

        /// <summary>Gets the names ticked, in the order of the list.</summary>
        public IReadOnlyList<string> Chosen => Items.Where(i => i.IsChecked).Select(i => i.Name).ToList();

        /// <summary>Gets whether OK is offered: one page setup at least is ticked.</summary>
        public bool CanOk => Items.Any(i => i.IsChecked);
    }
}
