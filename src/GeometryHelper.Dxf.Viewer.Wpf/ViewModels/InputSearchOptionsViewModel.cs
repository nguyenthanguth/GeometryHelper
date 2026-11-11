using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>A row of the Content type list of Input Search Options: a kind of named object and whether it is searched.</summary>
    public sealed class ContentTypeRowViewModel : ObservableObject
    {
        private bool _isSearched;

        internal ContentTypeRowViewModel(string name, bool isSearched)
        {
            Name = name;
            _isSearched = isSearched;
        }

        /// <summary>Gets the name, as Layer.</summary>
        public string Name { get; }

        /// <summary>Gets or sets whether Search Content lists the drawing's objects of the kind.</summary>
        public bool IsSearched
        {
            get => _isSearched;
            set => Set(ref _isSearched, value);
        }

        /// <inheritdoc/>
        public override string ToString() => Name;
    }

    /// <summary>
    /// AutoCAD 2021's Input Search Options of INPUTSEARCHOPTIONS, over a copy of the settings: Enable AutoComplete with
    /// Enable mid-string search and the order of the suggestions (According to frequency of usage, Alphabetically); Enable
    /// AutoCorrect with Remember corrections after 2 to 10 mistypes; Search system variables with Separate commands and system
    /// variables; the suggestion list's delay, 100 to 10 000 milliseconds; and Search content at command line with the Content
    /// type list, each kind ticked or not and moved up or down. An option under one switched off is greyed. OK writes them
    /// into the settings; Cancel leaves the settings as they were.
    /// </summary>
    public sealed class InputSearchOptionsViewModel : ObservableObject
    {
        private bool _autoComplete;
        private bool _midString;
        private bool _alphabetically;
        private bool _autoCorrect;
        private string _rememberAfter;
        private bool _systemVariables;
        private bool _separate;
        private string _delay;
        private bool _searchContent;
        private ContentTypeRowViewModel _selected;
        private string _message = string.Empty;

        /// <summary>Makes the dialog over the settings in use, which it reads and does not change.</summary>
        /// <param name="settings">The settings.</param>
        /// <exception cref="ArgumentNullException">Thrown when the settings are null.</exception>
        public InputSearchOptionsViewModel(GlobalSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            _autoComplete = settings.AutoComplete;
            _midString = settings.MidStringSearch;
            _alphabetically = settings.SortAlphabetically;
            _autoCorrect = settings.AutoCorrect;
            _rememberAfter = settings.AutoCorrectMistypes.ToString(CultureInfo.InvariantCulture);
            _systemVariables = settings.SearchSystemVariables;
            _separate = settings.SeparateSystemVariables;
            _delay = settings.AutoCompleteDelay.ToString(CultureInfo.InvariantCulture);
            _searchContent = settings.SearchContent;
            ContentTypes = new ObservableCollection<ContentTypeRowViewModel>(
                settings.ContentTypes.Select(t => new ContentTypeRowViewModel(t.Name, t.IsSearched))
            );
        }

        /// <summary>Gets or sets Enable AutoComplete; off, the options under it grey.</summary>
        public bool AutoComplete
        {
            get => _autoComplete;
            set
            {
                if (Set(ref _autoComplete, value))
                {
                    Raise(nameof(ByFrequency));
                }
            }
        }

        /// <summary>Gets or sets Enable mid-string search.</summary>
        public bool MidString
        {
            get => _midString;
            set => Set(ref _midString, value);
        }

        /// <summary>Gets or sets According to frequency of usage, the default order of the suggestions.</summary>
        public bool ByFrequency
        {
            get => !_alphabetically;
            set => Alphabetically = !value;
        }

        /// <summary>Gets or sets Alphabetically.</summary>
        public bool Alphabetically
        {
            get => _alphabetically;
            set
            {
                if (Set(ref _alphabetically, value))
                {
                    Raise(nameof(ByFrequency));
                }
            }
        }

        /// <summary>Gets or sets Enable AutoCorrect; off, Remember corrections greys.</summary>
        public bool AutoCorrect
        {
            get => _autoCorrect;
            set => Set(ref _autoCorrect, value);
        }

        /// <summary>Gets or sets the count of Remember corrections after … mistypes, as typed: 2 to 10.</summary>
        public string RememberAfter
        {
            get => _rememberAfter;
            set => Set(ref _rememberAfter, value ?? string.Empty);
        }

        /// <summary>Gets or sets Search system variables; off, Separate commands and system variables greys.</summary>
        public bool SystemVariables
        {
            get => _systemVariables;
            set => Set(ref _systemVariables, value);
        }

        /// <summary>Gets or sets Separate commands and system variables.</summary>
        public bool Separate
        {
            get => _separate;
            set => Set(ref _separate, value);
        }

        /// <summary>Gets or sets the suggestion list's delay in milliseconds, as typed: 100 to 10 000.</summary>
        public string Delay
        {
            get => _delay;
            set => Set(ref _delay, value ?? string.Empty);
        }

        /// <summary>Gets or sets Search content at command line; off, the Content type list greys.</summary>
        public bool SearchContent
        {
            get => _searchContent;
            set
            {
                if (Set(ref _searchContent, value))
                {
                    Raise(nameof(CanMoveUp));
                    Raise(nameof(CanMoveDown));
                }
            }
        }

        /// <summary>Gets the kinds of the Content type list, in the order Search Content lists them.</summary>
        public ObservableCollection<ContentTypeRowViewModel> ContentTypes { get; }

        /// <summary>Gets or sets the kind chosen, which the arrows move.</summary>
        public ContentTypeRowViewModel SelectedContentType
        {
            get => _selected;
            set
            {
                if (Set(ref _selected, value))
                {
                    Raise(nameof(CanMoveUp));
                    Raise(nameof(CanMoveDown));
                }
            }
        }

        /// <summary>Gets whether the up arrow is live: a kind chosen that is not the first, with Search content on.</summary>
        public bool CanMoveUp => _searchContent && _selected != null && ContentTypes.IndexOf(_selected) > 0;

        /// <summary>Gets whether the down arrow is live: a kind chosen that is not the last, with Search content on.</summary>
        public bool CanMoveDown =>
            _searchContent && _selected != null && ContentTypes.IndexOf(_selected) < ContentTypes.Count - 1;

        /// <summary>Gets why OK was refused, as "Remember corrections takes 2 to 10 mistypes."; empty where it was not.</summary>
        public string Message
        {
            get => _message;
            private set => Set(ref _message, value);
        }

        /// <summary>The up arrow: the kind chosen moved one place up.</summary>
        public void MoveUp() => Move(-1);

        /// <summary>The down arrow: the kind chosen moved one place down.</summary>
        public void MoveDown() => Move(1);

        /// <summary>OK: the numbers checked; true where the dialog may close, false with <see cref="Message"/> saying why not.</summary>
        /// <returns>Whether every value is in range.</returns>
        public bool Ok()
        {
            if (!TryWhole(_rememberAfter, 2, 10, out _))
            {
                Message = "Remember corrections takes 2 to 10 mistypes.";
                return false;
            }

            if (!TryWhole(_delay, 100, 10000, out _))
            {
                Message = "Requires an integer between 100 and 10000.";
                return false;
            }

            Message = string.Empty;
            return true;
        }

        /// <summary>The dialog's values written into settings, as OK keeps them; a number out of range is left as the settings have it.</summary>
        /// <param name="settings">The settings.</param>
        /// <exception cref="ArgumentNullException">Thrown when the settings are null.</exception>
        public void WriteInto(GlobalSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            settings.AutoComplete = _autoComplete;
            settings.MidStringSearch = _midString;
            settings.SortAlphabetically = _alphabetically;
            settings.AutoCorrect = _autoCorrect;
            settings.SearchSystemVariables = _systemVariables;
            settings.SeparateSystemVariables = _separate;
            settings.SearchContent = _searchContent;
            settings.ContentTypes = ContentTypes.Select(t => new GlobalContentType(t.Name, t.IsSearched)).ToList();

            if (TryWhole(_rememberAfter, 2, 10, out int remember))
            {
                settings.AutoCorrectMistypes = remember;
            }

            if (TryWhole(_delay, 100, 10000, out int delay))
            {
                settings.AutoCompleteDelay = delay;
            }
        }

        private void Move(int step)
        {
            int at = _selected == null ? -1 : ContentTypes.IndexOf(_selected);
            int to = at + step;

            if (!_searchContent || at < 0 || to < 0 || to >= ContentTypes.Count)
            {
                return;
            }

            ContentTypes.Move(at, to);
            Raise(nameof(CanMoveUp));
            Raise(nameof(CanMoveDown));
        }

        private static bool TryWhole(string text, int least, int most, out int value) =>
            int.TryParse((text ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
            && value >= least
            && value <= most;
    }
}
