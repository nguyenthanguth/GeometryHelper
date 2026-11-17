using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Input;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>One line of the AutoConstrain list of the Constraint Settings dialog: a constraint type with the box that lets AUTOCONSTRAIN apply it.</summary>
    public sealed class AutoConstrainRuleViewModel : ObservableObject
    {
        private bool _enabled;

        internal AutoConstrainRuleViewModel(CadConstraintType type, bool enabled)
        {
            Type = type;
            _enabled = enabled;
        }

        /// <summary>Gets the constraint type.</summary>
        public CadConstraintType Type { get; }

        /// <summary>Gets the name of the type as AutoCAD lists it.</summary>
        public string Name => Type.ToString();

        /// <summary>Gets or sets whether AUTOCONSTRAIN applies the type.</summary>
        public bool Enabled
        {
            get => _enabled;
            set => Set(ref _enabled, value);
        }
    }

    /// <summary>One box of the Geometric tab: a constraint type whose icons the constraint bars show.</summary>
    public sealed class BarTypeViewModel : ObservableObject
    {
        private bool _shown;

        internal BarTypeViewModel(CadConstraintType type, bool shown)
        {
            Type = type;
            _shown = shown;
        }

        /// <summary>Gets the constraint type.</summary>
        public CadConstraintType Type { get; }

        /// <summary>Gets the name of the type.</summary>
        public string Name => Type.ToString();

        /// <summary>Gets or sets whether the bars show the type.</summary>
        public bool Shown
        {
            get => _shown;
            set => Set(ref _shown, value);
        }
    }

    /// <summary>
    /// AutoCAD 2021's Constraint Settings dialog, which CONSTRAINTSETTINGS shows: the Geometric tab (infer constraints, the
    /// constraint bars shown after applying and for the selection, their transparency, the types the bars show), the
    /// Dimensional tab (the name format, the form of new dimensional constraints, the display of dynamic ones) and the
    /// AutoConstrain tab (the order and the types AUTOCONSTRAIN applies, its tolerances and its two boxes). OK gives the
    /// settings to the session.
    /// </summary>
    public sealed class ConstraintSettingsViewModel : ObservableObject
    {
        private readonly CadConstraintSettingsRequest _request;
        private int _tab;
        private bool _infer;
        private bool _barsOnApply;
        private bool _barsOnSelect;
        private double _transparency;
        private int _nameFormat;
        private bool _annotational;
        private bool _showDynamic;
        private string _distanceText;
        private string _angleText;
        private bool _tangentAtIntersection;
        private bool _perpendicularAtIntersection;
        private AutoConstrainRuleViewModel _selectedRule;

        /// <summary>Makes the dialog over what the command hands the window.</summary>
        /// <param name="request">The request: the settings to show and the tab to open on.</param>
        /// <exception cref="ArgumentNullException">Thrown when the request is null.</exception>
        public ConstraintSettingsViewModel(CadConstraintSettingsRequest request)
        {
            _request = request ?? throw new ArgumentNullException(nameof(request));
            CadConstraintSettings s = request.Settings;
            _tab = Math.Max(0, Math.Min(2, request.Tab));
            _infer = s.Infer;
            _barsOnApply = (s.BarDisplay & 2) != 0;
            _barsOnSelect = (s.BarDisplay & 1) != 0;
            _transparency = s.Transparency;
            _nameFormat = s.NameFormat;
            _annotational = s.Form == 1;
            _showDynamic = s.DynamicMode != 0;
            _distanceText = s.AutoDistance.ToString("0.####", CultureInfo.InvariantCulture);
            _angleText = s.AutoAngle.ToString("0.####", CultureInfo.InvariantCulture);
            _tangentAtIntersection = s.TangentAtIntersection;
            _perpendicularAtIntersection = s.PerpendicularAtIntersection;
            BarTypes = new ObservableCollection<BarTypeViewModel>(
                Enum.GetValues(typeof(CadConstraintType))
                    .Cast<CadConstraintType>()
                    .Select(t => new BarTypeViewModel(t, s.ShowsBarOf(t)))
            );
            Rules = new ObservableCollection<AutoConstrainRuleViewModel>(
                s.AutoRules.Select(r => new AutoConstrainRuleViewModel(r.Type, r.Enabled))
            );
            _selectedRule = Rules.FirstOrDefault();
            SelectAllCommand = new DelegateCommand(_ => SetBars(true));
            ClearAllCommand = new DelegateCommand(_ => SetBars(false));
            MoveUpCommand = new DelegateCommand(_ => Move(-1), _ => CanMove(-1));
            MoveDownCommand = new DelegateCommand(_ => Move(1), _ => CanMove(1));
        }

        /// <summary>Gets or sets the tab shown: 0 Geometric, 1 Dimensional, 2 AutoConstrain.</summary>
        public int Tab
        {
            get => _tab;
            set
            {
                if (Set(ref _tab, Math.Max(0, Math.Min(2, value))))
                {
                    Raise(nameof(IsGeometricTab));
                    Raise(nameof(IsDimensionalTab));
                    Raise(nameof(IsAutoConstrainTab));
                }
            }
        }

        /// <summary>Gets or sets whether the Geometric tab is shown.</summary>
        public bool IsGeometricTab
        {
            get => _tab == 0;
            set
            {
                if (value)
                {
                    Tab = 0;
                }
            }
        }

        /// <summary>Gets or sets whether the Dimensional tab is shown.</summary>
        public bool IsDimensionalTab
        {
            get => _tab == 1;
            set
            {
                if (value)
                {
                    Tab = 1;
                }
            }
        }

        /// <summary>Gets or sets whether the AutoConstrain tab is shown.</summary>
        public bool IsAutoConstrainTab
        {
            get => _tab == 2;
            set
            {
                if (value)
                {
                    Tab = 2;
                }
            }
        }

        /// <summary>Gets or sets whether a coincident, horizontal or vertical constraint is inferred where a line or arc is drawn (CONSTRAINTINFER).</summary>
        public bool InferConstraints
        {
            get => _infer;
            set => Set(ref _infer, value);
        }

        /// <summary>Gets or sets whether the bars of the objects a constraint was just applied to are shown (bit 2 of CONSTRAINTBARDISPLAY).</summary>
        public bool ShowBarsAfterApplying
        {
            get => _barsOnApply;
            set => Set(ref _barsOnApply, value);
        }

        /// <summary>Gets or sets whether the bars of the objects selected are shown (bit 1 of CONSTRAINTBARDISPLAY).</summary>
        public bool ShowBarsOfSelection
        {
            get => _barsOnSelect;
            set => Set(ref _barsOnSelect, value);
        }

        /// <summary>Gets or sets the transparency of the bars, in percent.</summary>
        public double Transparency
        {
            get => _transparency;
            set => Set(ref _transparency, Math.Max(0.0, Math.Min(90.0, value)));
        }

        /// <summary>Gets the boxes of the types the bars show.</summary>
        public ObservableCollection<BarTypeViewModel> BarTypes { get; }

        /// <summary>Gets the command that ticks every type.</summary>
        public ICommand SelectAllCommand { get; }

        /// <summary>Gets the command that clears every type.</summary>
        public ICommand ClearAllCommand { get; }

        /// <summary>Gets or sets the format of the words of a dimensional constraint: 0 name, 1 value, 2 name and expression.</summary>
        public int NameFormat
        {
            get => _nameFormat;
            set => Set(ref _nameFormat, Math.Max(0, Math.Min(2, value)));
        }

        /// <summary>Gets the choices of the name format, in the order of CONSTRAINTNAMEFORMAT.</summary>
        public IReadOnlyList<string> NameFormats { get; } = new[] { "Name", "Value", "Name and Expression" };

        /// <summary>Gets or sets whether new dimensional constraints are annotational (CCONSTRAINTFORM 1) rather than dynamic.</summary>
        public bool Annotational
        {
            get => _annotational;
            set
            {
                if (Set(ref _annotational, value))
                {
                    Raise(nameof(Dynamic));
                }
            }
        }

        /// <summary>Gets or sets whether new dimensional constraints are dynamic.</summary>
        public bool Dynamic
        {
            get => !_annotational;
            set => Annotational = !value;
        }

        /// <summary>Gets or sets whether dynamic dimensional constraints are shown (DYNCONSTRAINTMODE).</summary>
        public bool ShowDynamicConstraints
        {
            get => _showDynamic;
            set => Set(ref _showDynamic, value);
        }

        /// <summary>Gets or sets the distance tolerance of AUTOCONSTRAIN, as typed.</summary>
        public string DistanceText
        {
            get => _distanceText;
            set
            {
                if (Set(ref _distanceText, value))
                {
                    Raise(nameof(IsValid));
                    Raise(nameof(Message));
                }
            }
        }

        /// <summary>Gets or sets the angle tolerance of AUTOCONSTRAIN in degrees, as typed.</summary>
        public string AngleText
        {
            get => _angleText;
            set
            {
                if (Set(ref _angleText, value))
                {
                    Raise(nameof(IsValid));
                    Raise(nameof(Message));
                }
            }
        }

        /// <summary>Gets or sets whether tangent objects must share an intersection point.</summary>
        public bool TangentAtIntersection
        {
            get => _tangentAtIntersection;
            set => Set(ref _tangentAtIntersection, value);
        }

        /// <summary>Gets or sets whether perpendicular objects must share an intersection point.</summary>
        public bool PerpendicularAtIntersection
        {
            get => _perpendicularAtIntersection;
            set => Set(ref _perpendicularAtIntersection, value);
        }

        /// <summary>Gets the list of the types AUTOCONSTRAIN tries, in the order it tries them.</summary>
        public ObservableCollection<AutoConstrainRuleViewModel> Rules { get; }

        /// <summary>Gets or sets the line of the list selected, which the arrows move.</summary>
        public AutoConstrainRuleViewModel SelectedRule
        {
            get => _selectedRule;
            set
            {
                if (Set(ref _selectedRule, value))
                {
                    (MoveUpCommand as DelegateCommand)?.RaiseCanExecuteChanged();
                    (MoveDownCommand as DelegateCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>Gets the command that moves the selected line up: tried earlier.</summary>
        public ICommand MoveUpCommand { get; }

        /// <summary>Gets the command that moves the selected line down: tried later.</summary>
        public ICommand MoveDownCommand { get; }

        private void SetBars(bool shown)
        {
            foreach (BarTypeViewModel type in BarTypes)
            {
                type.Shown = shown;
            }
        }

        private bool CanMove(int by)
        {
            int at = _selectedRule == null ? -1 : Rules.IndexOf(_selectedRule);
            return at >= 0 && at + by >= 0 && at + by < Rules.Count;
        }

        private void Move(int by)
        {
            if (!CanMove(by))
            {
                return;
            }

            AutoConstrainRuleViewModel rule = _selectedRule;
            int at = Rules.IndexOf(rule);
            Rules.Move(at, at + by);
            SelectedRule = rule;
        }

        private bool TryNumber(string text, out double value) =>
            double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && value >= 0.0;

        /// <summary>Gets whether both tolerances are numbers of nought or more, which OK needs.</summary>
        public bool IsValid => TryNumber(_distanceText, out _) && TryNumber(_angleText, out _);

        /// <summary>Gets what is wrong with the dialog; empty where all is well.</summary>
        public string Message =>
            !TryNumber(_distanceText, out _) ? "The distance tolerance must be a number, nought or more."
            : !TryNumber(_angleText, out _) ? "The angle tolerance must be a number, nought or more."
            : string.Empty;

        /// <summary>Gives the settings the dialog holds to the request, as OK does, and sets its action to OK.</summary>
        /// <exception cref="InvalidOperationException">Thrown when a tolerance is not valid.</exception>
        public void Commit()
        {
            if (!TryNumber(_distanceText, out double distance) || !TryNumber(_angleText, out double angle))
            {
                throw new InvalidOperationException(Message);
            }

            CadConstraintSettings s = _request.Settings;
            s.Infer = _infer;
            s.BarDisplay = (_barsOnSelect ? 1 : 0) | (_barsOnApply ? 2 : 0);
            s.Transparency = (int)Math.Round(_transparency);
            int mode = 0;

            foreach (BarTypeViewModel type in BarTypes)
            {
                if (type.Shown)
                {
                    mode |= 1 << (int)type.Type;
                }
            }

            s.BarMode = mode;
            s.NameFormat = _nameFormat;
            s.Form = _annotational ? 1 : 0;
            s.DynamicMode = _showDynamic ? 1 : 0;
            s.AutoDistance = distance;
            s.AutoAngle = angle;
            s.TangentAtIntersection = _tangentAtIntersection;
            s.PerpendicularAtIntersection = _perpendicularAtIntersection;
            s.AutoRules = Rules.Select(r => new CadAutoConstrainRule(r.Type, r.Enabled)).ToList();
            _request.Action = CadDialogAction.Ok;
        }
    }
}
