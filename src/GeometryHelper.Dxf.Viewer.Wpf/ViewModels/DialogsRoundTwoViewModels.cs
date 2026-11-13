using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// AutoCAD 2021's Rename dialog: the Named Objects list, the Items of the kind picked, Old Name, and Rename To with the
    /// name typed. Rename To renames the item in the list at once; OK hands every rename to RENAME, which makes them as one
    /// step of undo. A name refused is said in <see cref="Message"/> and nothing is listed.
    /// </summary>
    public sealed class RenameViewModel : ObservableObject
    {
        private const string BadCharacters = "<>/\\\":;?*|,=`";
        private readonly CadRenameRequest _request;
        private readonly Dictionary<string, ObservableCollection<string>> _items =
            new Dictionary<string, ObservableCollection<string>>();
        private string _kind;
        private string _item;
        private string _newName = string.Empty;
        private string _message = string.Empty;

        /// <summary>Makes the dialog over RENAME's request.</summary>
        /// <param name="request">The request.</param>
        public RenameViewModel(CadRenameRequest request)
        {
            _request = request ?? throw new ArgumentNullException(nameof(request));

            foreach (string kind in request.Kinds)
            {
                _items[kind] = new ObservableCollection<string>(request.NamesOf(kind));
            }

            _kind = request.Kinds.FirstOrDefault();
        }

        /// <summary>Gets the kinds of named objects.</summary>
        public IReadOnlyList<string> Kinds => _request.Kinds;

        /// <summary>Gets or sets the kind picked in Named Objects; its items are listed.</summary>
        public string Kind
        {
            get => _kind;
            set
            {
                if (Set(ref _kind, value))
                {
                    Item = null;
                    Raise(nameof(Items));
                }
            }
        }

        /// <summary>Gets the names of the kind picked, with the renames made in the dialog.</summary>
        public IReadOnlyList<string> Items =>
            _kind != null && _items.TryGetValue(_kind, out ObservableCollection<string> items)
                ? items
                : (IReadOnlyList<string>)Array.Empty<string>();

        /// <summary>Gets or sets the item picked; its name is the Old Name, and Rename To starts from it.</summary>
        public string Item
        {
            get => _item;
            set
            {
                if (Set(ref _item, value))
                {
                    Raise(nameof(OldName));
                    NewName = value ?? string.Empty;
                    Message = string.Empty;
                }
            }
        }

        /// <summary>Gets the name of the item picked.</summary>
        public string OldName => _item ?? string.Empty;

        /// <summary>Gets or sets the name typed beside Rename To.</summary>
        public string NewName
        {
            get => _newName;
            set => Set(ref _newName, value ?? string.Empty);
        }

        /// <summary>Gets the line saying why the last Rename To was refused; empty where it was not.</summary>
        public string Message
        {
            get => _message;
            private set => Set(ref _message, value);
        }

        /// <summary>Gets the renames made so far, in order.</summary>
        public IReadOnlyList<CadRename> Renames => _request.Renames.ToList();

        /// <summary>
        /// Rename To: the item picked takes the name typed in the list, the rename kept for OK; refused for no item, an empty
        /// name, one with any of &lt; &gt; / \ " : ; ? * | , = ` in it, or the name of another item of the kind.
        /// </summary>
        /// <returns>Whether it was renamed.</returns>
        public bool RenameTo()
        {
            string wanted = _newName.Trim();

            if (_item == null)
            {
                Message = "Pick an item to rename.";
                return false;
            }

            if (wanted.Length == 0 || wanted.IndexOfAny(BadCharacters.ToCharArray()) >= 0)
            {
                Message = "Invalid name.";
                return false;
            }

            ObservableCollection<string> items = _items[_kind];

            if (string.Equals(wanted, _item, StringComparison.Ordinal))
            {
                Message = string.Empty;
                return false;
            }

            string other = items.FirstOrDefault(n =>
                string.Equals(n, wanted, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(n, _item, StringComparison.OrdinalIgnoreCase)
            );

            if (other != null)
            {
                Message = "\"" + other + "\" already exists.";
                return false;
            }

            _request.Renames.Add(new CadRename(_kind, _item, wanted));
            int at = items.IndexOf(_item);
            items.RemoveAt(at);
            int to = 0;

            while (
                to < items.Count
                && (
                    string.Compare(items[to], wanted, StringComparison.OrdinalIgnoreCase) < 0
                    || (
                        string.Compare(items[to], wanted, StringComparison.OrdinalIgnoreCase) == 0
                        && string.CompareOrdinal(items[to], wanted) < 0
                    )
                )
            )
            {
                to++;
            }

            items.Insert(to, wanted);
            _item = wanted;
            Raise(nameof(Item));
            Raise(nameof(OldName));
            Raise(nameof(Renames));
            Message = string.Empty;
            return true;
        }

        /// <summary>OK: the renames are made once the dialog closes.</summary>
        public void Accept() => _request.IsAccepted = true;
    }

    /// <summary>A line of the Lineweights list: the lineweight, the words it is listed by and the pixels its sample is drawn.</summary>
    public sealed class LineweightItem
    {
        internal LineweightItem(DxfLineWeight weight, string text, double pixels)
        {
            Weight = weight;
            Text = text;
            Pixels = pixels;
        }

        /// <summary>Gets the lineweight.</summary>
        public DxfLineWeight Weight { get; }

        /// <summary>Gets the words it is listed by: ByLayer, ByBlock, Default, "0.25 mm" or "0.010"".</summary>
        public string Text { get; }

        /// <summary>Gets how thick its sample is drawn, in pixels; nought for ByLayer, ByBlock and Default, which have none.</summary>
        public double Pixels { get; }
    }

    /// <summary>
    /// AutoCAD 2021's Lineweight Settings dialog: the Lineweights list, whose line picked is the lineweight of new objects
    /// (CELWEIGHT); Units for Listing (LWUNITS), Display Lineweight (LWDISPLAY), Default (LWDEFAULT) and Adjust Display
    /// Scale, and the Current Lineweight line.
    /// </summary>
    public sealed class LineweightSettingsViewModel : ObservableObject
    {
        private DxfLineWeight _current;
        private bool _inches;
        private bool _display;
        private int _default;
        private double _scale;
        private IReadOnlyList<LineweightItem> _items;

        /// <summary>Makes the dialog over the settings it shows.</summary>
        /// <param name="current">The lineweight of new objects, CELWEIGHT.</param>
        /// <param name="display">Whether lineweights are drawn, LWDISPLAY.</param>
        /// <param name="inches">Whether they are listed in inches, LWUNITS 0.</param>
        /// <param name="defaultWeight">The Default lineweight in hundredths of a millimetre, LWDEFAULT.</param>
        /// <param name="scale">The display scale, 0.5 to 2.</param>
        public LineweightSettingsViewModel(
            DxfLineWeight current,
            bool display,
            bool inches,
            int defaultWeight,
            double scale
        )
        {
            _current = current;
            _display = display;
            _inches = inches;
            _default = GlobalSettings.Lineweights.Contains(defaultWeight) ? defaultWeight : 25;
            _scale = Math.Max(0.5, Math.Min(2.0, scale));
            _items = MakeItems();
        }

        /// <summary>Gets the Lineweights list: ByLayer, ByBlock, Default, then AutoCAD's 24 widths.</summary>
        public IReadOnlyList<LineweightItem> Items => _items;

        /// <summary>Gets or sets the line picked, the lineweight of new objects.</summary>
        public LineweightItem Selected
        {
            get => _items.FirstOrDefault(i => i.Weight == _current);
            set
            {
                if (value != null && value.Weight != _current)
                {
                    _current = value.Weight;
                    Raise(nameof(Selected));
                    Raise(nameof(CurrentText));
                }
            }
        }

        /// <summary>Gets the lineweight of new objects picked.</summary>
        public DxfLineWeight Current => _current;

        /// <summary>Gets or sets whether the widths are listed in inches; false lists them in millimetres.</summary>
        public bool IsInches
        {
            get => _inches;
            set
            {
                if (Set(ref _inches, value))
                {
                    Raise(nameof(IsMillimetres));
                    _items = MakeItems();
                    Raise(nameof(Items));
                    Raise(nameof(Selected));
                    Raise(nameof(DefaultItems));
                    Raise(nameof(DefaultText));
                    Raise(nameof(CurrentText));
                }
            }
        }

        /// <summary>Gets or sets whether the widths are listed in millimetres, the other choice of Units for Listing.</summary>
        public bool IsMillimetres
        {
            get => !_inches;
            set => IsInches = !value;
        }

        /// <summary>Gets or sets Display Lineweight.</summary>
        public bool DisplayLineweight
        {
            get => _display;
            set => Set(ref _display, value);
        }

        /// <summary>Gets the widths Default offers, in the units listed.</summary>
        public IReadOnlyList<string> DefaultItems => GlobalSettings.Lineweights.Select(Width).ToList();

        /// <summary>Gets or sets the Default width as listed.</summary>
        public string DefaultText
        {
            get => Width(_default);
            set
            {
                int found = GlobalSettings.Lineweights.FirstOrDefault(w => Width(w) == value);

                if (value != null && Width(found) == value && found != _default)
                {
                    _default = found;
                    Raise(nameof(DefaultText));
                    _items = MakeItems();
                    Raise(nameof(Items));
                    Raise(nameof(Selected));
                }
            }
        }

        /// <summary>Gets the Default width, in hundredths of a millimetre.</summary>
        public int DefaultWeight => _default;

        /// <summary>Gets or sets where the Adjust Display Scale slider stands, 0 (Min) to 10 (Max), 5 in the middle a scale of 1.</summary>
        public double ScalePosition
        {
            get => Math.Round(5.0 + 5.0 * Math.Log(_scale, 2.0), 6);
            set
            {
                double scale = Math.Pow(2.0, (Math.Max(0.0, Math.Min(10.0, value)) - 5.0) / 5.0);

                if (Set(ref _scale, Math.Round(scale, 6), nameof(Scale)))
                {
                    Raise(nameof(ScalePosition));
                    _items = MakeItems();
                    Raise(nameof(Items));
                    Raise(nameof(Selected));
                }
            }
        }

        /// <summary>Gets the display scale, 0.5 to 2.</summary>
        public double Scale => _scale;

        /// <summary>Gets the Current Lineweight line.</summary>
        public string CurrentText => "Current Lineweight: " + Named(_current);

        private IReadOnlyList<LineweightItem> MakeItems()
        {
            var items = new List<LineweightItem>
            {
                new LineweightItem(DxfLineWeight.ByLayer, "ByLayer", 0.0),
                new LineweightItem(DxfLineWeight.ByBlock, "ByBlock", 0.0),
                new LineweightItem(DxfLineWeight.ByLineWeightDefault, "Default", 0.0),
            };

            foreach (int width in GlobalSettings.Lineweights)
            {
                items.Add(
                    new LineweightItem(
                        (DxfLineWeight)width,
                        Width(width),
                        Math.Max(1.0, width / 100.0 * 96.0 / 25.4 * _scale)
                    )
                );
            }

            return items;
        }

        private string Named(DxfLineWeight weight)
        {
            switch (weight)
            {
                case DxfLineWeight.ByLayer:
                    return "ByLayer";
                case DxfLineWeight.ByBlock:
                    return "ByBlock";
                case DxfLineWeight.ByLineWeightDefault:
                    return "Default";
                default:
                    return (int)weight >= 0 ? Width((int)weight) : "ByLayer";
            }
        }

        /// <summary>A width as listed: "0.25 mm", or in inches as AutoCAD lists them, "0.010"".</summary>
        private string Width(int hundredths) =>
            _inches
                ? (hundredths / 2540.0).ToString("0.000", CultureInfo.InvariantCulture) + "\""
                : (hundredths / 100.0).ToString("0.00", CultureInfo.InvariantCulture) + " mm";
    }

    /// <summary>
    /// AutoCAD 2021's Check Spelling dialog over SPELL's <see cref="CadSpellCheck"/>: Where to check with its Select objects
    /// button, Start, Not in dictionary, Suggestions with the replacement typed, Ignore, Ignore All, Change, Change All, Add to
    /// Dictionary, the main and custom dictionaries, and Close.
    /// </summary>
    public sealed class CheckSpellingViewModel : ObservableObject
    {
        private string _replacement = string.Empty;
        private string _suggestion;
        private string _dictionaryFile = string.Empty;
        private string _message = string.Empty;

        /// <summary>Makes the dialog over SPELL's check.</summary>
        /// <param name="check">The check.</param>
        public CheckSpellingViewModel(CadSpellCheck check)
        {
            Check = check ?? throw new ArgumentNullException(nameof(check));
            _dictionaryFile = check.CustomDictionary ?? string.Empty;
        }

        /// <summary>Gets the check.</summary>
        public CadSpellCheck Check { get; }

        /// <summary>Gets the places Where to check offers.</summary>
        public IReadOnlyList<string> Places => CadSpellCheck.Places;

        /// <summary>Gets or sets where to check.</summary>
        public string Where
        {
            get => Check.Where;
            set
            {
                Check.Where = value;
                Raise(nameof(Where));
                Raise(nameof(CanSelectObjects));
            }
        }

        /// <summary>Gets whether the Select objects button is on: where to check is the objects selected.</summary>
        public bool CanSelectObjects => Check.Where == CadSpellCheck.SelectedObjects;

        /// <summary>Gets the main dictionary, the one the app holds.</summary>
        public IReadOnlyList<string> MainDictionaries { get; } = new[] { "English (built in)" };

        /// <summary>Gets the word not in the dictionary.</summary>
        public string Word => Check.Word;

        /// <summary>Gets the text it is in.</summary>
        public string Context => Check.Context;

        /// <summary>Gets the words nearest it.</summary>
        public IReadOnlyList<string> Suggestions => Check.Suggestions;

        /// <summary>Gets or sets the suggestion picked; it becomes the replacement.</summary>
        public string Suggestion
        {
            get => _suggestion;
            set
            {
                if (Set(ref _suggestion, value) && value != null)
                {
                    Replacement = value;
                }
            }
        }

        /// <summary>Gets or sets the replacement typed above the suggestions.</summary>
        public string Replacement
        {
            get => _replacement;
            set => Set(ref _replacement, value ?? string.Empty);
        }

        /// <summary>Gets whether a word waits, so that the answer buttons are on.</summary>
        public bool IsChecking => Check.IsChecking;

        /// <summary>Gets the line under the buttons: the check's status, or a dictionary refused.</summary>
        public string Status => _message.Length > 0 ? _message : Check.Status;

        /// <summary>Gets or sets the custom dictionary file typed.</summary>
        public string DictionaryFile
        {
            get => _dictionaryFile;
            set => Set(ref _dictionaryFile, value ?? string.Empty);
        }

        /// <summary>Reads the custom dictionary file typed; none for an empty name.</summary>
        /// <returns>Whether it was read.</returns>
        public bool LoadDictionary()
        {
            string path = _dictionaryFile.Trim().Trim('"');

            if (path.Length == 0 || string.Equals(path, Check.CustomDictionary, StringComparison.OrdinalIgnoreCase))
            {
                return path.Length > 0;
            }

            _message = Check.LoadDictionary(path) ?? string.Empty;
            Raise(nameof(Status));
            return _message.Length == 0;
        }

        /// <summary>Start: the check from the first text where to check says.</summary>
        public void Start()
        {
            LoadDictionary();
            _message = string.Empty;
            Check.Start();
            Refresh();
        }

        /// <summary>Ignore.</summary>
        public void Ignore()
        {
            Check.Ignore();
            Refresh();
        }

        /// <summary>Ignore All.</summary>
        public void IgnoreAll()
        {
            Check.IgnoreAll();
            Refresh();
        }

        /// <summary>Change: the word changed to the replacement, or the first suggestion where none is typed.</summary>
        public void Change()
        {
            Check.Change(_replacement);
            Refresh();
        }

        /// <summary>Change All.</summary>
        public void ChangeAll()
        {
            Check.ChangeAll(_replacement);
            Refresh();
        }

        /// <summary>Add to Dictionary.</summary>
        public void AddToDictionary()
        {
            Check.AddToDictionary();
            Refresh();
        }

        /// <summary>Select objects: the dialog closes to have them picked, and comes back checking them.</summary>
        public void SelectObjects()
        {
            LoadDictionary();
            Check.IsSelecting = true;
        }

        private void Refresh()
        {
            _suggestion = null;
            _replacement = Check.Suggestions.Count > 0 ? Check.Suggestions[0] : string.Empty;
            Raise(nameof(Word));
            Raise(nameof(Context));
            Raise(nameof(Suggestions));
            Raise(nameof(Suggestion));
            Raise(nameof(Replacement));
            Raise(nameof(IsChecking));
            Raise(nameof(Status));
        }
    }

    /// <summary>
    /// AutoCAD 2021's Boundary Creation dialog over BOUNDARY's request: Pick Points, Island detection, Boundary retention with
    /// its object type, and the boundary set, which is every object of the space.
    /// </summary>
    public sealed class BoundaryCreationViewModel : ObservableObject
    {
        private readonly CadBoundaryRequest _request;
        private bool _islands;
        private string _objectType;

        /// <summary>Makes the dialog over the request.</summary>
        /// <param name="request">The request.</param>
        public BoundaryCreationViewModel(CadBoundaryRequest request)
        {
            _request = request ?? throw new ArgumentNullException(nameof(request));
            _islands = request.IslandDetection;
            _objectType = request.IsRegion ? "Region" : "Polyline";
        }

        /// <summary>Gets the object types: Polyline and Region.</summary>
        public IReadOnlyList<string> ObjectTypes { get; } = new[] { "Polyline", "Region" };

        /// <summary>Gets or sets the object type.</summary>
        public string ObjectType
        {
            get => _objectType;
            set => Set(ref _objectType, value == "Region" ? "Region" : "Polyline");
        }

        /// <summary>Gets or sets Island detection.</summary>
        public bool IslandDetection
        {
            get => _islands;
            set => Set(ref _islands, value);
        }

        /// <summary>Gets the boundary sets offered: the current viewport, every object of the space.</summary>
        public IReadOnlyList<string> BoundarySets { get; } = new[] { "Current viewport" };

        /// <summary>Pick Points: the settings kept and the points asked once the dialog closes.</summary>
        public void PickPoints()
        {
            _request.IslandDetection = _islands;
            _request.IsRegion = _objectType == "Region";
            _request.IsPicking = true;
        }
    }
}
