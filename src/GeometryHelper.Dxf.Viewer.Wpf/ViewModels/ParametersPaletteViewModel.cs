using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>One row of the Parameters Manager: a name, an expression and the value it comes to; the name and the expression are edited in the row.</summary>
    public sealed class ParameterRowViewModel : ObservableObject
    {
        private readonly ParametersPaletteViewModel _owner;
        private string _name;
        private string _expression;

        internal ParameterRowViewModel(ParametersPaletteViewModel owner, CadParameterRow row, int luprec, int auprec)
        {
            _owner = owner;
            _name = row.Name;
            _expression = row.Expression;
            IsUser = row.IsUser;
            IsAngle = row.IsAngle;
            Value = row.Value.ToString(
                "F" + Math.Max(0, row.IsAngle ? auprec : luprec).ToString(CultureInfo.InvariantCulture),
                CultureInfo.InvariantCulture
            );
        }

        /// <summary>Gets or sets the name; changed in the row, the parameter is renamed.</summary>
        public string Name
        {
            get => _name;
            set
            {
                string old = _name;

                if (value != null && value != old && Set(ref _name, value))
                {
                    _owner.Renamed(old, value);
                }
            }
        }

        /// <summary>Gets or sets the expression; changed in the row, the parameter takes it and what it drives moves.</summary>
        public string Expression
        {
            get => _expression;
            set
            {
                if (value != null && value != _expression && Set(ref _expression, value))
                {
                    _owner.Edited(_name, value);
                }
            }
        }

        /// <summary>Gets the value the expression comes to, in the precision of the drawing's units.</summary>
        public string Value { get; }

        /// <summary>Gets whether the parameter is a user parameter, which can be deleted.</summary>
        public bool IsUser { get; }

        /// <summary>Gets whether the value is an angle in degrees.</summary>
        public bool IsAngle { get; }

        /// <summary>Gets the kind of the parameter: "User" or "Dimensional".</summary>
        public string Kind => IsUser ? "User" : "Dimensional";
    }

    /// <summary>
    /// AutoCAD 2021's Parameters Manager palette (PARAMETERS): the parameters of the working space of the drawing in a list of
    /// name, expression and value, edited in place, with New User Parameter and Delete. Each edit is one step of undo and is
    /// made through <see cref="CadParameters"/>; where one is refused the reason is shown under the list and the row put back.
    /// </summary>
    public sealed class ParametersPaletteViewModel : ObservableObject
    {
        private CadDocument _document;
        private ParameterRowViewModel _selected;
        private string _status = string.Empty;
        private bool _refreshing;

        /// <summary>Makes the palette.</summary>
        public ParametersPaletteViewModel()
        {
            NewCommand = new DelegateCommand(_ => AddUserParameter(), _ => _document != null);
            DeleteCommand = new DelegateCommand(
                _ => DeleteSelected(),
                _ => _selected != null && _selected.IsUser && _document != null
            );
        }

        /// <summary>Gets the rows, in the order the drawing keeps the parameters.</summary>
        public ObservableCollection<ParameterRowViewModel> Rows { get; } =
            new ObservableCollection<ParameterRowViewModel>();

        /// <summary>Gets or sets the document whose parameters are listed; null for none. The list follows its changes.</summary>
        public CadDocument Document
        {
            get => _document;
            set
            {
                if (ReferenceEquals(_document, value))
                {
                    return;
                }

                if (_document != null)
                {
                    _document.Changed -= OnChanged;
                }

                _document = value;

                if (_document != null)
                {
                    _document.Changed += OnChanged;
                }

                Raise(nameof(HasDocument));
                Refresh();
            }
        }

        /// <summary>Gets whether a document is shown.</summary>
        public bool HasDocument => _document != null;

        /// <summary>Gets or sets the row selected.</summary>
        public ParameterRowViewModel Selected
        {
            get => _selected;
            set
            {
                if (Set(ref _selected, value))
                {
                    (DeleteCommand as DelegateCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>Gets the line under the list: why the last edit was refused; empty where it was made.</summary>
        public string Status
        {
            get => _status;
            private set => Set(ref _status, value);
        }

        /// <summary>Gets the command that makes a user parameter, named user1, user2 ... and valued 1.</summary>
        public ICommand NewCommand { get; }

        /// <summary>Gets the command that deletes the user parameter selected.</summary>
        public ICommand DeleteCommand { get; }

        private void OnChanged(object sender, CadDocumentChangedEventArgs e) => Refresh();

        /// <summary>Reads the parameters of the document again.</summary>
        public void Refresh()
        {
            _refreshing = true;

            try
            {
                string kept = _selected?.Name;
                Rows.Clear();

                if (_document != null)
                {
                    int luprec = _document.Database.Luprec;
                    int auprec = _document.Database.Auprec;

                    foreach (CadParameterRow row in CadParameters.Rows(_document))
                    {
                        Rows.Add(new ParameterRowViewModel(this, row, luprec, auprec));
                    }
                }

                Selected = kept == null ? null : Rows.FirstOrDefault(r => r.Name == kept);
            }
            finally
            {
                _refreshing = false;
            }
        }

        internal void Edited(string name, string expression)
        {
            if (_refreshing || _document == null)
            {
                return;
            }

            Report(CadParameters.Edit(_document, name, expression));
        }

        internal void Renamed(string old, string name)
        {
            if (_refreshing || _document == null)
            {
                return;
            }

            Report(CadParameters.Rename(_document, old, name));
        }

        private void AddUserParameter()
        {
            string name = "user1";

            for (int i = 2; Rows.Any(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase)); i++)
            {
                name = "user" + i.ToString(CultureInfo.InvariantCulture);
            }

            Report(CadParameters.New(_document, name, "1"));
        }

        private void DeleteSelected()
        {
            if (_selected != null)
            {
                Report(CadParameters.Delete(_document, _selected.Name));
            }
        }

        private void Report(CadEditResult result)
        {
            Status = result.Succeeded ? string.Empty : result.Message ?? string.Empty;

            // A refused edit leaves the row as typed: the list is read again from the drawing.
            Refresh();
        }
    }
}
