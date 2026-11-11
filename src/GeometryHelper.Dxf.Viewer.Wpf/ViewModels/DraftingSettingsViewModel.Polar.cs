using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The Polar Tracking tab of Drafting Settings past its on box and increment: Additional angles with their list, New and
    /// Delete (POLARADDANG and POLARMODE 4), Object Snap Tracking Settings (POLARMODE 2) and Polar Angle measurement
    /// (POLARMODE 1), as AutoCAD 2021's tab. Apply sets POLARMODE and POLARADDANG as typing them does, its bit 8 (Shift to
    /// acquire) kept as it was.
    /// </summary>
    public sealed partial class DraftingSettingsViewModel
    {
        /// <summary>The most additional angles AutoCAD keeps in POLARADDANG.</summary>
        private const int MostAdditionalAngles = 10;

        private int _polarMode;
        private bool _isAdditionalAngles;
        private bool _isTrackingAllPolar;
        private bool _isRelativeToLastSegment;
        private string _selectedAdditionalAngle;
        private bool _isTypingAngle;
        private string _newAngle = string.Empty;
        private string _angleError = string.Empty;

        /// <summary>Gets or sets "Additional angles": the list of additional angles is used (POLARMODE 4).</summary>
        public bool IsAdditionalAngles
        {
            get => _isAdditionalAngles;
            set
            {
                if (Set(ref _isAdditionalAngles, value))
                {
                    if (!value && _isTypingAngle)
                    {
                        DropNewAngle();
                    }

                    RaiseAngleButtons();
                }
            }
        }

        /// <summary>Gets the additional angles listed, as POLARADDANG keeps them, in their order; "34", "120", "350".</summary>
        public ObservableCollection<string> AdditionalAngles { get; } = new ObservableCollection<string>();

        /// <summary>Gets or sets the additional angle chosen in the list, which Delete takes out; null for none.</summary>
        public string SelectedAdditionalAngle
        {
            get => _selectedAdditionalAngle;
            set
            {
                if (Set(ref _selectedAdditionalAngle, value))
                {
                    RaiseAngleButtons();
                }
            }
        }

        /// <summary>Gets whether New can add an angle: additional angles on, fewer than ten listed, none being typed.</summary>
        public bool CanAddAngle =>
            _isAdditionalAngles && !_isTypingAngle && AdditionalAngles.Count < MostAdditionalAngles;

        /// <summary>Gets whether Delete can take out the angle chosen.</summary>
        public bool CanDeleteAngle => _isAdditionalAngles && !_isTypingAngle && _selectedAdditionalAngle != null;

        /// <summary>Gets whether a new angle is being typed in the edit row at the end of the list.</summary>
        public bool IsTypingAngle
        {
            get => _isTypingAngle;
            private set
            {
                if (Set(ref _isTypingAngle, value))
                {
                    RaiseAngleButtons();
                }
            }
        }

        /// <summary>Gets or sets the text of the edit row New opened.</summary>
        public string NewAngle
        {
            get => _newAngle;
            set => Set(ref _newAngle, value ?? string.Empty);
        }

        /// <summary>Gets why the angle typed was not kept, under the list; empty while it is not refused.</summary>
        public string AngleError
        {
            get => _angleError;
            private set => Set(ref _angleError, value ?? string.Empty);
        }

        /// <summary>Gets or sets "Track using all polar angle settings" (POLARMODE 2); false is "Track orthogonally only".</summary>
        public bool IsTrackingAllPolar
        {
            get => _isTrackingAllPolar;
            set
            {
                if (Set(ref _isTrackingAllPolar, value))
                {
                    Raise(nameof(IsTrackingOrthogonally));
                }
            }
        }

        /// <summary>Gets or sets "Track orthogonally only", the other choice of <see cref="IsTrackingAllPolar"/>.</summary>
        public bool IsTrackingOrthogonally
        {
            get => !_isTrackingAllPolar;
            set => IsTrackingAllPolar = !value;
        }

        /// <summary>Gets or sets "Relative to last segment" (POLARMODE 1); false is "Absolute".</summary>
        public bool IsRelativeToLastSegment
        {
            get => _isRelativeToLastSegment;
            set
            {
                if (Set(ref _isRelativeToLastSegment, value))
                {
                    Raise(nameof(IsAbsolute));
                }
            }
        }

        /// <summary>Gets or sets "Absolute", the other choice of <see cref="IsRelativeToLastSegment"/>.</summary>
        public bool IsAbsolute
        {
            get => !_isRelativeToLastSegment;
            set => IsRelativeToLastSegment = !value;
        }

        /// <summary>Gets the command of New: an edit row at the end of the list, as <see cref="NewAngleRow"/>.</summary>
        public ICommand NewAngleCommand { get; private set; }

        /// <summary>Gets the command of Delete: the angle chosen taken out, as <see cref="DeleteAngle"/>.</summary>
        public ICommand DeleteAngleCommand { get; private set; }

        /// <summary>New: opens the edit row at the end of the list, empty; nothing while <see cref="CanAddAngle"/> is false.</summary>
        public void NewAngleRow()
        {
            if (!CanAddAngle)
            {
                return;
            }

            NewAngle = string.Empty;
            AngleError = string.Empty;
            IsTypingAngle = true;
        }

        /// <summary>
        /// Enter in the edit row, or a click elsewhere: the angle typed is listed where it reads as an angle in the drawing's
        /// angle units ("15", "22.5", "45d"), the row closed; else the row stays open with <see cref="AngleError"/> said. An
        /// empty row is dropped.
        /// </summary>
        /// <returns>Whether the row is closed.</returns>
        public bool KeepNewAngle()
        {
            if (!_isTypingAngle)
            {
                return true;
            }

            string typed = _newAngle.Trim();

            if (typed.Length == 0)
            {
                DropNewAngle();
                return true;
            }

            if (typed.IndexOf(';') >= 0 || !CadUnits.TryAngle(typed, Aunits, out _))
            {
                AngleError = "Not an angle. Type a number in the drawing’s angle units, for example 15 or 22.5.";
                return false;
            }

            AdditionalAngles.Add(typed);
            AngleError = string.Empty;
            IsTypingAngle = false;
            SelectedAdditionalAngle = typed;
            return true;
        }

        /// <summary>Esc in the edit row: the row dropped, nothing listed.</summary>
        public void DropNewAngle()
        {
            NewAngle = string.Empty;
            AngleError = string.Empty;
            IsTypingAngle = false;
        }

        /// <summary>Delete: the angle chosen taken out of the list.</summary>
        public void DeleteAngle()
        {
            if (!CanDeleteAngle)
            {
                return;
            }

            AdditionalAngles.Remove(_selectedAdditionalAngle);
            SelectedAdditionalAngle = null;
            RaiseAngleButtons();
        }

        private int Aunits => _model.Document?.Database.Aunits ?? 0;

        /// <summary>The tab's state read from POLARMODE and POLARADDANG, and its commands made.</summary>
        private void LoadPolar()
        {
            GlobalSettings settings = _model.Settings;
            _polarMode = settings.PolarMode;
            _isRelativeToLastSegment = (_polarMode & 1) != 0;
            _isTrackingAllPolar = (_polarMode & 2) != 0;
            _isAdditionalAngles = (_polarMode & 4) != 0;

            if (!string.IsNullOrEmpty(settings.PolarAddedAngles))
            {
                foreach (string angle in settings.PolarAddedAngles.Split(';'))
                {
                    AdditionalAngles.Add(angle);
                }
            }

            NewAngleCommand = new DelegateCommand(_ => NewAngleRow());
            DeleteAngleCommand = new DelegateCommand(_ => DeleteAngle());
        }

        /// <summary>
        /// Checks the tab before Apply takes it: an angle being typed is kept first, and refused, stops Apply.
        /// </summary>
        /// <returns>Whether the tab can be applied.</returns>
        private bool CheckPolar()
        {
            if (KeepNewAngle())
            {
                return true;
            }

            Notice = "An additional angle is not an angle.";
            return false;
        }

        /// <summary>POLARADDANG from the list and POLARMODE from the tab's choices, bit 8 kept, as typing them sets them.</summary>
        private void ApplyPolar()
        {
            _model.SetPolarAddedAngles(string.Join(";", AdditionalAngles.ToArray()));
            int mode =
                (_polarMode & 8)
                | (_isRelativeToLastSegment ? 1 : 0)
                | (_isTrackingAllPolar ? 2 : 0)
                | (_isAdditionalAngles ? 4 : 0);
            _model.SetPolarMode(mode);
            _polarMode = mode;
        }

        private void RaiseAngleButtons()
        {
            Raise(nameof(CanAddAngle));
            Raise(nameof(CanDeleteAngle));
        }
    }
}
