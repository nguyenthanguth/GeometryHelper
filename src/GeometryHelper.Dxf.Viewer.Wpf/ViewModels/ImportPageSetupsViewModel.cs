using System;
using System.Collections.Generic;
using System.Linq;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>A page setup of another drawing in the Import Page Setups list, ticked to be copied.</summary>
    public sealed class ImportPageSetupRow : ObservableObject
    {
        private bool _isChecked;

        internal ImportPageSetupRow(string name, bool isChecked)
        {
            Name = name;
            _isChecked = isChecked;
        }

        /// <summary>Gets the name of the page setup in the other drawing.</summary>
        public string Name { get; }

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
    /// Import Page Setups, from Import… of the Page Setup Manager: the page setups of the drawing chosen, each ticked to be
    /// copied into this one. All are ticked to begin with; OK is offered while one is.
    /// </summary>
    public sealed class ImportPageSetupsViewModel : ObservableObject
    {
        /// <summary>Makes the dialog on the page setups of a drawing.</summary>
        /// <param name="fileName">The drawing, as its name is shown over the list.</param>
        /// <param name="names">The page setups it has, by name.</param>
        /// <exception cref="ArgumentNullException">Thrown when the file name or the names are null.</exception>
        public ImportPageSetupsViewModel(string fileName, IEnumerable<string> names)
        {
            FileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
            Items = (names ?? throw new ArgumentNullException(nameof(names)))
                .Select(n => new ImportPageSetupRow(n, true))
                .ToList();

            foreach (ImportPageSetupRow row in Items)
            {
                row.PropertyChanged += (s, e) =>
                {
                    Raise(nameof(CanOk));
                    Raise(nameof(Chosen));
                };
            }
        }

        /// <summary>Gets the drawing the page setups come from.</summary>
        public string FileName { get; }

        /// <summary>Gets the title.</summary>
        public string Title => "Import Page Setups";

        /// <summary>Gets the words over the list: the drawing's name.</summary>
        public string FromText => "Page setups in " + System.IO.Path.GetFileName(FileName) + ":";

        /// <summary>Gets the page setups of the drawing, each with its box.</summary>
        public IReadOnlyList<ImportPageSetupRow> Items { get; }

        /// <summary>Gets the names ticked, in the order of the list.</summary>
        public IReadOnlyList<string> Chosen => Items.Where(i => i.IsChecked).Select(i => i.Name).ToList();

        /// <summary>Gets whether OK is offered: one page setup at least is ticked.</summary>
        public bool CanOk => Items.Any(i => i.IsChecked);
    }
}
