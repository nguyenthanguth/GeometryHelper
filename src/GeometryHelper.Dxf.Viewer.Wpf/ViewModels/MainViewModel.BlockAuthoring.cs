using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The logic of the Visibility States dialog of BVSTATE, as AutoCAD 2021's: the states of the block open in the Block Editor in
    /// their order, the one chosen, and New (named, showing none, all, or what the current state shows), Rename, Delete, Move Up,
    /// Move Down and Set Current. Each change is made through the document (one undo step) and the list read again; a refusal is
    /// said in <see cref="Message"/>, in AutoCAD's words.
    /// </summary>
    public sealed class VisibilityStatesViewModel : ObservableObject
    {
        private readonly CadDocument _document;
        private IReadOnlyList<string> _states = Array.Empty<string>();
        private string _selected;
        private string _name = string.Empty;
        private string _how = HideAll;
        private string _message = string.Empty;

        /// <summary>The option of a new state that shows no object.</summary>
        public const string HideAll = "Hide all objects in new state";

        /// <summary>The option of a new state that shows every object.</summary>
        public const string ShowAll = "Show all objects in new state";

        /// <summary>The option of a new state that shows what the current state shows.</summary>
        public const string LeaveAsIs = "Leave visibility of objects unchanged in new state";

        /// <summary>Makes the dialog's logic over a document whose Block Editor is open.</summary>
        /// <param name="document">The document.</param>
        /// <exception cref="ArgumentNullException">Thrown when the document is null.</exception>
        public VisibilityStatesViewModel(CadDocument document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            NewCommand = new DelegateCommand(_ => New(), _ => CanName);
            RenameCommand = new DelegateCommand(_ => Rename(), _ => CanName && _selected != null);
            DeleteCommand = new DelegateCommand(_ => Delete(), _ => _selected != null);
            MoveUpCommand = new DelegateCommand(_ => Move(-1), _ => _selected != null && Index > 0);
            MoveDownCommand = new DelegateCommand(_ => Move(1), _ => _selected != null && Index < _states.Count - 1);
            SetCurrentCommand = new DelegateCommand(_ => SetCurrent(), _ => _selected != null);
            Refresh();
            Selected = Current;
        }

        /// <summary>Gets the title of the dialog.</summary>
        public string Title => "Visibility States";

        /// <summary>Gets the options a new state starts from, in AutoCAD's words.</summary>
        public IReadOnlyList<string> Options { get; } = new[] { HideAll, ShowAll, LeaveAsIs };

        /// <summary>Gets the states of the block, in their order.</summary>
        public IReadOnlyList<string> States => _states;

        /// <summary>Gets the state the editor shows.</summary>
        public string Current => _document.BlockEditor?.VisibilityState;

        /// <summary>Gets or sets the state chosen in the list.</summary>
        public string Selected
        {
            get => _selected;
            set
            {
                if (Set(ref _selected, value))
                {
                    if (value != null)
                    {
                        Name = value;
                    }

                    Raise(nameof(CanName));
                }
            }
        }

        /// <summary>Gets or sets the name typed, for New and Rename; the chosen state's own where one is chosen.</summary>
        public string Name
        {
            get => _name;
            set
            {
                if (Set(ref _name, value ?? string.Empty))
                {
                    Raise(nameof(CanName));
                }
            }
        }

        /// <summary>Gets or sets what a new state shows.</summary>
        public string How
        {
            get => _how;
            set => Set(ref _how, value ?? HideAll);
        }

        /// <summary>Gets the last refusal, "Cannot delete the current visibility state."; empty where none.</summary>
        public string Message
        {
            get => _message;
            private set => Set(ref _message, value ?? string.Empty);
        }

        /// <summary>Gets whether a name is typed.</summary>
        public bool CanName => !string.IsNullOrWhiteSpace(_name);

        /// <summary>Gets the command of New.</summary>
        public ICommand NewCommand { get; }

        /// <summary>Gets the command of Rename.</summary>
        public ICommand RenameCommand { get; }

        /// <summary>Gets the command of Delete.</summary>
        public ICommand DeleteCommand { get; }

        /// <summary>Gets the command of Move Up.</summary>
        public ICommand MoveUpCommand { get; }

        /// <summary>Gets the command of Move Down.</summary>
        public ICommand MoveDownCommand { get; }

        /// <summary>Gets the command of Set Current.</summary>
        public ICommand SetCurrentCommand { get; }

        private int Index => _selected == null ? -1 : _states.ToList().FindIndex(s => s == _selected);

        /// <summary>Reads the list of states again, after a change.</summary>
        private void Refresh()
        {
            _states = _document.BlockEditor?.VisibilityStates ?? Array.Empty<string>();
            Raise(nameof(States));
            Raise(nameof(Current));
        }

        private void Done(CadEditResult result, string select)
        {
            Message = result.Succeeded ? string.Empty : result.Message;
            Refresh();
            Selected = _states.Contains(select) ? select : _states.FirstOrDefault();
            CommandManager.InvalidateRequerySuggested();
        }

        private void New()
        {
            string how =
                _how == ShowAll ? "Show all"
                : _how == LeaveAsIs ? "Current visibility"
                : "Hide all";
            string name = _name.Trim();
            Done(_document.AddVisibilityState(name, how), name);
        }

        private void Rename()
        {
            string to = _name.Trim();
            Done(_document.RenameVisibilityState(_selected, to), to);
        }

        private void Delete()
        {
            Done(_document.DeleteVisibilityStates(new[] { _selected }), null);
        }

        private void Move(int places)
        {
            string state = _selected;
            Done(_document.MoveVisibilityState(state, places), state);
        }

        private void SetCurrent()
        {
            string state = _selected;
            CadEditResult result = _document.SetBlockEditorVisibilityState(state);
            Done(result, state);
        }
    }

    /// <summary>One button of the Block Authoring Palettes: a parameter or an action, with the command it starts.</summary>
    public sealed class AuthoringPaletteItemViewModel
    {
        internal AuthoringPaletteItemViewModel(string name, string toolTip, bool isEnabled, Action run)
        {
            Name = name;
            ToolTip = toolTip;
            IsEnabled = isEnabled;
            Command = new DelegateCommand(_ => run(), _ => isEnabled);
        }

        /// <summary>Gets the name shown.</summary>
        public string Name { get; }

        /// <summary>Gets what the button does.</summary>
        public string ToolTip { get; }

        /// <summary>Gets whether the button is live.</summary>
        public bool IsEnabled { get; }

        /// <summary>Gets the command that starts BPARAMETER or BACTION with the kind answered.</summary>
        public ICommand Command { get; }
    }

    /// <summary>
    /// The logic of the Block Authoring Palettes (BAUTHORPALETTE): a tab of the parameters and one of the actions, each a button
    /// that starts BPARAMETER or BACTION with its kind already answered, as a tool of AutoCAD's palette does.
    /// </summary>
    public sealed class AuthoringPaletteViewModel
    {
        internal AuthoringPaletteViewModel(MainViewModel model)
        {
            Parameters = BlockAuthoringKinds
                .Parameters.Select(kind => new AuthoringPaletteItemViewModel(
                    kind[0],
                    kind[0] != "Lookup" ? "BPARAMETER " + kind[1] : BlockAuthoringKinds.NeedsTable,
                    kind[0] != "Lookup",
                    () => model.StartCommandWithOption("BPARAMETER " + kind[1])
                ))
                .ToList();
            Actions = BlockAuthoringKinds
                .Actions.Select(kind => new AuthoringPaletteItemViewModel(
                    kind[0],
                    kind[0] != "Lookup" ? "BACTION " + kind[1] : BlockAuthoringKinds.NeedsTable,
                    kind[0] != "Lookup",
                    () => model.StartCommandWithOption("BACTION " + kind[1])
                ))
                .ToList();
        }

        /// <summary>Gets the title of the palettes.</summary>
        public string Title => "Block Authoring Palettes";

        /// <summary>Gets the parameters tab: one button for each kind.</summary>
        public IReadOnlyList<AuthoringPaletteItemViewModel> Parameters { get; }

        /// <summary>Gets the actions tab: one button for each kind.</summary>
        public IReadOnlyList<AuthoringPaletteItemViewModel> Actions { get; }
    }

    /// <summary>The parameters and actions the Block Editor tab and the palettes list: the name shown and the keyword BPARAMETER or BACTION takes it by.</summary>
    internal static class BlockAuthoringKinds
    {
        /// <summary>The words of a grey Lookup: its table is not in this version.</summary>
        internal const string NeedsTable = "Needs the lookup table, which is not in this version";

        internal static readonly string[][] Parameters =
        {
            new[] { "Point", "pOint" },
            new[] { "Linear", "Linear" },
            new[] { "Polar", "Polar" },
            new[] { "XY", "Xy" },
            new[] { "Rotation", "Rotation" },
            new[] { "Alignment", "Alignment" },
            new[] { "Flip", "Flip" },
            new[] { "Visibility", "Visibility" },
            new[] { "Lookup", "looKup" },
            new[] { "Base Point", "Base" },
        };

        internal static readonly string[][] Actions =
        {
            new[] { "Move", "Move" },
            new[] { "Scale", "Scale" },
            new[] { "Stretch", "sTretch" },
            new[] { "Polar Stretch", "Polar" },
            new[] { "Rotate", "Rotate" },
            new[] { "Flip", "Flip" },
            new[] { "Array", "Array" },
            new[] { "Lookup", "Lookup" },
        };
    }
}
