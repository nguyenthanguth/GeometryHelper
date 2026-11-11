using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// AutoCAD's Quick Select dialog, which QSELECT shows: Apply to, Object type, Properties, Operator, Value, how to apply
    /// and Append. Its logic is <see cref="CadQuickSelect"/>; OK selects and says "N item(s) selected." on the command line.
    /// </summary>
    public sealed class QuickSelectViewModel : ObservableObject
    {
        /// <summary>The operators in the order of AutoCAD's list.</summary>
        public static readonly IReadOnlyList<string> OperatorNames = new[]
        {
            "= Equals",
            "<> Not Equal",
            "> Greater than",
            "< Less than",
            "* Wildcard Match",
            "Select All",
        };

        private readonly CadDocument _document;
        private readonly CadCommandSession _session;
        private CadQuickSelect _quick;
        private string _applyTo;
        private string _objectType;
        private string _property;
        private string _operator = OperatorNames[0];
        private string _value = string.Empty;
        private bool _include = true;
        private bool _append;

        /// <summary>Makes the dialog over a document, writing on a session's command line.</summary>
        /// <param name="document">The document.</param>
        /// <param name="session">The session whose command line is written; null for none.</param>
        public QuickSelectViewModel(CadDocument document, CadCommandSession session)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _session = session;
            var applyTo = new List<string> { "Entire drawing" };

            if (document.Selection.Count > 0)
            {
                applyTo.Add("Current selection");
            }

            ApplyToOptions = applyTo;
            ApplyTo = applyTo[applyTo.Count - 1];
        }

        /// <summary>Gets what Apply to offers: Entire drawing, and Current selection where objects are selected.</summary>
        public IReadOnlyList<string> ApplyToOptions { get; }

        /// <summary>Gets or sets Apply to; the types, properties and values follow.</summary>
        public string ApplyTo
        {
            get => _applyTo;
            set
            {
                if (Set(ref _applyTo, value))
                {
                    _quick = new CadQuickSelect(
                        _document,
                        null,
                        value == "Current selection" ? _document.Selection.Handles : null
                    );
                    ObjectTypes = _quick.ObjectTypes;
                    Raise(nameof(ObjectTypes));
                    ObjectType = ObjectTypes.FirstOrDefault();
                }
            }
        }

        /// <summary>Gets the object types in scope, Multiple first.</summary>
        public IReadOnlyList<string> ObjectTypes { get; private set; } = Array.Empty<string>();

        /// <summary>Gets or sets the object type.</summary>
        public string ObjectType
        {
            get => _objectType;
            set
            {
                if (Set(ref _objectType, value))
                {
                    Properties = value == null ? Array.Empty<string>() : _quick.PropertiesOf(value);
                    Raise(nameof(Properties));
                    string property = Properties.Contains("Layer") ? "Layer" : Properties.FirstOrDefault();

                    // The values in use are those of the new type, though the property is the one it was.
                    if (!Set(ref _property, property, nameof(Property)))
                    {
                        Raise(nameof(Property));
                    }

                    ReadValues();
                }
            }
        }

        /// <summary>Gets the properties of the object type.</summary>
        public IReadOnlyList<string> Properties { get; private set; } = Array.Empty<string>();

        /// <summary>Gets or sets the property.</summary>
        public string Property
        {
            get => _property;
            set
            {
                if (Set(ref _property, value))
                {
                    ReadValues();
                }
            }
        }

        /// <summary>The values in use of the property among the objects of the type, the first offered.</summary>
        private void ReadValues()
        {
            Values =
                _property == null || _objectType == null
                    ? Array.Empty<string>()
                    : _quick.ValuesOf(_objectType, _property);
            Raise(nameof(Values));
            Raise(nameof(IsValueListed));
            Value = Values.FirstOrDefault() ?? string.Empty;
        }

        /// <summary>Gets the operators.</summary>
        public IReadOnlyList<string> Operators => OperatorNames;

        /// <summary>Gets or sets the operator, as its list writes it: "= Equals".</summary>
        public string Operator
        {
            get => _operator;
            set
            {
                if (Set(ref _operator, value))
                {
                    Raise(nameof(IsValueShown));
                }
            }
        }

        /// <summary>Gets the values in use of a listed property: colours, layers, linetypes, lineweights.</summary>
        public IReadOnlyList<string> Values { get; private set; } = Array.Empty<string>();

        /// <summary>Gets whether Value is a list rather than typed.</summary>
        public bool IsValueListed => CadQuickSelect.IsListed(_property) && _operator != "* Wildcard Match";

        /// <summary>Gets whether Value is asked: not for Select All.</summary>
        public bool IsValueShown => _operator != "Select All";

        /// <summary>Gets or sets the value.</summary>
        public string Value
        {
            get => _value;
            set => Set(ref _value, value ?? string.Empty);
        }

        /// <summary>Gets or sets whether the objects that match are included in the new selection; false excludes them.</summary>
        public bool Include
        {
            get => _include;
            set
            {
                if (Set(ref _include, value))
                {
                    Raise(nameof(Exclude));
                }
            }
        }

        /// <summary>Gets or sets whether the objects that do not match make the new selection.</summary>
        public bool Exclude
        {
            get => !_include;
            set => Include = !value;
        }

        /// <summary>Gets or sets whether the new selection is added to the selection there is.</summary>
        public bool Append
        {
            get => _append;
            set => Set(ref _append, value);
        }

        /// <summary>Gets the note at the foot of the dialog.</summary>
        public string Note =>
            "OK selects the objects that match and reports on the command line: \"N item(s) selected.\" Value lists the colours, layers and linetypes in use; numbers are typed.";

        /// <summary>OK: selects the objects that match, as the options say, and writes "N item(s) selected." on the command line.</summary>
        /// <returns>The line written.</returns>
        public string Apply()
        {
            var op = (CadQuickSelectOperator)Math.Max(0, OperatorNames.ToList().IndexOf(_operator));
            IReadOnlyList<string> matched =
                _objectType == null
                    ? Array.Empty<string>()
                    : _quick.Match(
                        _objectType,
                        _property,
                        op,
                        _value,
                        _include,
                        _session?.Tolerance ?? Tolerance.Default
                    );
            string line = _quick.Apply(matched, _append);
            _session?.Write(line);
            return line;
        }
    }

    /// <summary>A row of the results of Find and Replace: where, the type of object and the text.</summary>
    public sealed class FindResultRowViewModel : ObservableObject
    {
        internal FindResultRowViewModel(CadFindResult result)
        {
            Result = result;
        }

        /// <summary>Gets the result.</summary>
        public CadFindResult Result { get; }

        /// <summary>Gets where the object is: Model, a layout, a block.</summary>
        public string Location => Result.Location;

        /// <summary>Gets the type of object.</summary>
        public string ObjectType => Result.ObjectType;

        /// <summary>Gets the text, on one line.</summary>
        public string Text => Result.Text.Replace("\r", " ").Replace("\n", " ");
    }

    /// <summary>
    /// AutoCAD's Find and Replace dialog, which FIND shows: Find what, Replace with, Find where, the list of results, and the
    /// Search Options and Text Types. Its logic is <see cref="CadFind"/>; Replace and Replace All are each one undo step, Zoom
    /// shows the result highlighted and Select selects the results highlighted.
    /// </summary>
    public sealed class FindReplaceViewModel : ObservableObject
    {
        private readonly CadDocument _document;
        private readonly CadCommandSession _session;
        private readonly IReadOnlyList<string> _selected;
        private readonly Action<IReadOnlyList<string>> _zoom;
        private string _findWhat = string.Empty;
        private string _replaceWith = string.Empty;
        private string _findWhere;
        private bool _listResults = true;
        private FindResultRowViewModel _current;
        private string _searched;
        private string _summary = string.Empty;

        /// <summary>Makes the dialog over a document.</summary>
        /// <param name="document">The document.</param>
        /// <param name="session">The session whose command line is written; null for none.</param>
        /// <param name="selected">The objects selected before FIND, Find where: Selected objects; null for none.</param>
        /// <param name="zoom">Shows objects in the view, as Zoom does; null for none.</param>
        public FindReplaceViewModel(
            CadDocument document,
            CadCommandSession session,
            IReadOnlyList<string> selected,
            Action<IReadOnlyList<string>> zoom
        )
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _session = session;
            _selected = selected ?? Array.Empty<string>();
            _zoom = zoom;
            var where = new List<string> { "Entire drawing", "Current space/layout" };

            if (_selected.Count > 0)
            {
                where.Add("Selected objects");
            }

            FindWhereOptions = where;
            _findWhere = where[where.Count - 1 == 2 ? 2 : 0];
            Options = new CadFindOptions();
        }

        /// <summary>Gets the options, the checkboxes of Search Options and Text Types bound to them.</summary>
        public CadFindOptions Options { get; }

        /// <summary>Gets or sets whether letters match only in the same case.</summary>
        public bool MatchCase
        {
            get => Options.MatchCase;
            set
            {
                Options.MatchCase = value;
                Changed();
            }
        }

        /// <summary>Gets or sets Find whole words only.</summary>
        public bool WholeWords
        {
            get => Options.WholeWords;
            set
            {
                Options.WholeWords = value;
                Changed();
            }
        }

        /// <summary>Gets or sets Use wildcards.</summary>
        public bool UseWildcards
        {
            get => Options.UseWildcards;
            set
            {
                Options.UseWildcards = value;
                Changed();
            }
        }

        /// <summary>Gets or sets Search blocks.</summary>
        public bool SearchBlocks
        {
            get => Options.SearchBlocks;
            set
            {
                Options.SearchBlocks = value;
                Changed();
            }
        }

        /// <summary>Gets or sets Ignore hidden items.</summary>
        public bool IgnoreHidden
        {
            get => Options.IgnoreHidden;
            set
            {
                Options.IgnoreHidden = value;
                Changed();
            }
        }

        /// <summary>Gets or sets Block attribute value.</summary>
        public bool BlockAttributes
        {
            get => Options.BlockAttributes;
            set
            {
                Options.BlockAttributes = value;
                Changed();
            }
        }

        /// <summary>Gets or sets Dimension or leader text.</summary>
        public bool DimensionText
        {
            get => Options.DimensionText;
            set
            {
                Options.DimensionText = value;
                Changed();
            }
        }

        /// <summary>Gets or sets Single-line or multiline text.</summary>
        public bool Text
        {
            get => Options.Text;
            set
            {
                Options.Text = value;
                Changed();
            }
        }

        /// <summary>Gets or sets Table text.</summary>
        public bool TableText
        {
            get => Options.TableText;
            set
            {
                Options.TableText = value;
                Changed();
            }
        }

        /// <summary>Gets or sets the text searched for.</summary>
        public string FindWhat
        {
            get => _findWhat;
            set
            {
                if (Set(ref _findWhat, value ?? string.Empty))
                {
                    Changed();
                }
            }
        }

        /// <summary>Gets or sets the text put in its place.</summary>
        public string ReplaceWith
        {
            get => _replaceWith;
            set => Set(ref _replaceWith, value ?? string.Empty);
        }

        /// <summary>Gets what Find where offers: Entire drawing, Current space/layout, and Selected objects where objects were selected.</summary>
        public IReadOnlyList<string> FindWhereOptions { get; }

        /// <summary>Gets or sets Find where.</summary>
        public string FindWhere
        {
            get => _findWhere;
            set
            {
                if (Set(ref _findWhere, value))
                {
                    Changed();
                }
            }
        }

        /// <summary>Gets or sets whether the results are listed.</summary>
        public bool ListResults
        {
            get => _listResults;
            set => Set(ref _listResults, value);
        }

        /// <summary>Gets the results of the last search.</summary>
        public ObservableCollection<FindResultRowViewModel> Results { get; } =
            new ObservableCollection<FindResultRowViewModel>();

        /// <summary>Gets or sets the result highlighted.</summary>
        public FindResultRowViewModel Current
        {
            get => _current;
            set => Set(ref _current, value);
        }

        /// <summary>Gets the line under the results: "6 matches in 6 objects · double-click a row to zoom to it".</summary>
        public string Summary
        {
            get => _summary;
            private set => Set(ref _summary, value);
        }

        /// <summary>Find Next: searches where the text or the options changed since, then highlights the next result and zooms to it.</summary>
        /// <returns>Whether a result is highlighted.</returns>
        public bool FindNext()
        {
            if (_searched == null)
            {
                Search();
            }

            if (Results.Count == 0)
            {
                return false;
            }

            int at = _current == null ? 0 : (Results.IndexOf(_current) + 1) % Results.Count;
            Current = Results[at];
            Zoom();
            return true;
        }

        /// <summary>Replace: the text replaced in the result highlighted, one undo step, then the search made again.</summary>
        /// <returns>What came of it; null with no result highlighted.</returns>
        public CadEditResult Replace()
        {
            if (_current == null && !FindNext())
            {
                return null;
            }

            int at = Results.IndexOf(_current);
            CadEditResult result = CadFind.Replace(_document, _current.Result, _findWhat, _replaceWith, Options);
            Search();

            if (Results.Count > 0)
            {
                Current = Results[Math.Min(Math.Max(at, 0), Results.Count - 1)];
            }

            return result;
        }

        /// <summary>Replace All: the text replaced in every result, one undo step, saying how many on the command line.</summary>
        /// <returns>What came of it.</returns>
        public CadEditResult ReplaceAll()
        {
            Search();
            int matches = Results.Sum(r => r.Result.Count);
            CadEditResult result = CadFind.ReplaceAll(
                _document,
                Results.Select(r => r.Result).ToList(),
                _findWhat,
                _replaceWith,
                Options
            );

            if (result.Succeeded)
            {
                _session?.Write(matches.ToString(CultureInfo.InvariantCulture) + " match(es) replaced.");
            }

            Search();
            return result;
        }

        /// <summary>Zoom: shows the object of the result highlighted.</summary>
        public void Zoom()
        {
            if (_current != null)
            {
                _zoom?.Invoke(new[] { _current.Result.Handle });
            }
        }

        /// <summary>Select: selects the objects of every result listed, as AutoCAD's Create Selection Set.</summary>
        /// <returns>How many objects were selected.</returns>
        public int Select()
        {
            var handles = new SortedSet<string>(Results.Select(r => r.Result.Handle), StringComparer.OrdinalIgnoreCase);
            _document.Selection.Set(handles);
            return handles.Count;
        }

        /// <summary>Searches again, as the text and the options stand.</summary>
        public void Search()
        {
            CadFindScope scope =
                _findWhere == "Selected objects" ? CadFindScope.SelectedObjects
                : _findWhere == "Current space/layout" ? CadFindScope.CurrentSpace
                : CadFindScope.EntireDrawing;
            Results.Clear();
            Current = null;

            foreach (CadFindResult result in CadFind.Find(_document, _findWhat, Options, scope, _selected))
            {
                Results.Add(new FindResultRowViewModel(result));
            }

            int objects = Results.Select(r => r.Result.Handle).Distinct(StringComparer.OrdinalIgnoreCase).Count();
            int matches = Results.Sum(r => r.Result.Count);
            Summary =
                matches.ToString(CultureInfo.InvariantCulture)
                + " matches in "
                + objects.ToString(CultureInfo.InvariantCulture)
                + " objects · double-click a row to zoom to it";
            _searched = _findWhat;
        }

        /// <summary>The text or an option changed: the next Find Next searches again.</summary>
        private void Changed()
        {
            _searched = null;
        }
    }
}
