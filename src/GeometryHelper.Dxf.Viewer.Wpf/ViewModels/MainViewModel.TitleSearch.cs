using System;
using System.Collections.Generic;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The search box of the title bar, as AutoCAD 2021's "Type a keyword or phrase" box takes it for the commands: the text
    /// typed lists the commands and variables whose name or description holds it, as the application menu's search does
    /// (<see cref="SearchCommands"/>), and choosing a row, or Enter for the first, starts the command and empties the box. The
    /// help topics and the Autodesk community AutoCAD's box also searches are not here: only the presenter's own commands.
    /// </summary>
    public sealed partial class MainViewModel
    {
        private string _titleSearchText = string.Empty;
        private IReadOnlyList<CommandSuggestionViewModel> _titleSearchResults =
            Array.Empty<CommandSuggestionViewModel>();

        /// <summary>Gets or sets the text of the title bar's search box; the list below it follows, and is empty for no text.</summary>
        public string TitleSearchText
        {
            get => _titleSearchText;
            set
            {
                if (Set(ref _titleSearchText, (value ?? string.Empty).Trim()))
                {
                    TitleSearchResults =
                        _titleSearchText.Length == 0
                            ? Array.Empty<CommandSuggestionViewModel>()
                            : SearchCommands(_titleSearchText);
                    Raise(nameof(IsTitleSearching));
                }
            }
        }

        /// <summary>Gets whether the search box holds text: the list of commands is shown below it.</summary>
        public bool IsTitleSearching => _titleSearchText.Length > 0;

        /// <summary>Gets the commands the search found, as the command line's AutoComplete orders them; none where no drawing is open.</summary>
        public IReadOnlyList<CommandSuggestionViewModel> TitleSearchResults
        {
            get => _titleSearchResults;
            private set => Set(ref _titleSearchResults, value);
        }

        /// <summary>Runs a command the title bar's search found, and empties the box.</summary>
        /// <param name="row">The row; null for the first of the list.</param>
        /// <returns>False where there was no row to run.</returns>
        public bool RunTitleSearchResult(CommandSuggestionViewModel row = null)
        {
            row = row ?? (_titleSearchResults.Count > 0 ? _titleSearchResults[0] : null);

            if (row == null || row.IsHeader)
            {
                return false;
            }

            TitleSearchText = string.Empty;
            return RunSuggestion(row);
        }
    }
}
