using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>A check box of the Object Snap tab: an object snap of AutoCAD's with its marker, off and greyed where it is not built yet.</summary>
    public sealed class SnapModeRowViewModel : ObservableObject
    {
        private bool _isChecked;

        internal SnapModeRowViewModel(CadSnapKind kind, CadSnapModes mode, bool isChecked)
        {
            Kind = kind;
            Mode = mode;
            _isChecked = isChecked && mode != CadSnapModes.None;
        }

        /// <summary>Gets the name, as "Geometric Center".</summary>
        public string Name => CadSnapMarker.NameOf(Kind);

        /// <summary>Gets the snap kind, whose marker is drawn beside the name.</summary>
        public CadSnapKind Kind { get; }

        /// <summary>Gets the flag of the snap; <see cref="CadSnapModes.None"/> for one not built yet.</summary>
        public CadSnapModes Mode { get; }

        /// <summary>Gets whether the snap is built, so that its box can be ticked.</summary>
        public bool IsAvailable => Mode != CadSnapModes.None;

        /// <summary>Gets the tooltip: "Not built yet." for a snap not built; null for one built.</summary>
        public string ToolTip => IsAvailable ? null : MainViewModel.NotBuiltYet;

        /// <summary>Gets or sets whether the snap is ticked; a snap not built stays off.</summary>
        public bool IsChecked
        {
            get => _isChecked;
            set => Set(ref _isChecked, value && IsAvailable);
        }

        /// <inheritdoc/>
        public override string ToString() => Name + (IsChecked ? " on" : " off");
    }

    /// <summary>
    /// AutoCAD's Drafting Settings (DSETTINGS, DS, OS) on what the presenter has: Snap and Grid (snap on, its X and Y
    /// spacing, grid on), Polar Tracking (on, the increment angle) and Object Snap (Object Snap On, Object Snap Tracking On,
    /// the fourteen snaps with their markers, Select All and Clear All). Apply gives them to the window at once, OK applies
    /// and closes, Cancel drops them; the snaps ticked are kept in the settings file by the window.
    /// </summary>
    public sealed class DraftingSettingsViewModel : ObservableObject
    {
        /// <summary>The tab of the snap grid.</summary>
        public const string SnapAndGridTab = "Snap and Grid";

        /// <summary>The tab of polar tracking.</summary>
        public const string PolarTrackingTab = "Polar Tracking";

        /// <summary>The tab of the object snaps.</summary>
        public const string ObjectSnapTab = "Object Snap";

        private static readonly string[] Increments = { "90", "45", "30", "22.5", "18", "15", "10", "5" };

        private readonly MainViewModel _model;
        private string _tab;
        private bool _isSnapOn;
        private bool _isGridSnapOn;
        private bool _isGridOn;
        private bool _isPolarOn;
        private bool _isTrackingOn;
        private string _snapX;
        private string _snapY;
        private string _increment;
        private string _notice = string.Empty;

        internal DraftingSettingsViewModel(MainViewModel model, string tab)
        {
            _model = model;
            _tab = new[] { SnapAndGridTab, PolarTrackingTab, ObjectSnapTab }.FirstOrDefault(t =>
                string.Equals(t, tab, StringComparison.OrdinalIgnoreCase)
            ) ?? ObjectSnapTab;
            Modes = MainViewModel
                .SnapModeTable.Select(i => new SnapModeRowViewModel(i.Kind, i.Mode, (model.SnapModes & i.Mode) != 0))
                .ToList();
            _isSnapOn = model.Toggles.First(t => t.Name == "OSNAP").IsOn;
            _isGridSnapOn = model.Toggles.First(t => t.Name == "SNAP").IsOn;
            _isGridOn = model.Toggles.First(t => t.Name == "GRID").IsOn;
            _isPolarOn = model.Toggles.First(t => t.Name == "POLAR").IsOn;
            _isTrackingOn = model.Toggles.First(t => t.Name == "OTRACK").IsOn;
            _snapX = Text(model.Snap.GridX);
            _snapY = Text(model.Snap.GridY);
            _increment = Text(model.Snap.PolarAngle * 180.0 / Math.PI);
            SelectAllCommand = new DelegateCommand(_ => SetAll(true));
            ClearAllCommand = new DelegateCommand(_ => SetAll(false));
            ApplyCommand = new DelegateCommand(_ => Apply());
            OkCommand = new DelegateCommand(_ => Ok());
            CancelCommand = new DelegateCommand(_ => CloseRequested?.Invoke(this, false));
        }

        /// <summary>Raised when the dialog is to close: true after OK, false after Cancel.</summary>
        public event EventHandler<bool> CloseRequested;

        /// <summary>Gets the names of the tabs, in AutoCAD's order.</summary>
        public IReadOnlyList<string> Tabs { get; } = new[] { SnapAndGridTab, PolarTrackingTab, ObjectSnapTab };

        /// <summary>Gets or sets the tab shown.</summary>
        public string Tab
        {
            get => _tab;
            set
            {
                if (Tabs.Contains(value) && Set(ref _tab, value))
                {
                    Raise(nameof(IsSnapAndGridTab));
                    Raise(nameof(IsPolarTrackingTab));
                    Raise(nameof(IsObjectSnapTab));
                }
            }
        }

        /// <summary>Gets or sets whether Snap and Grid is shown; set true, it is.</summary>
        public bool IsSnapAndGridTab
        {
            get => _tab == SnapAndGridTab;
            set => Tab = value ? SnapAndGridTab : _tab;
        }

        /// <summary>Gets or sets whether Polar Tracking is shown; set true, it is.</summary>
        public bool IsPolarTrackingTab
        {
            get => _tab == PolarTrackingTab;
            set => Tab = value ? PolarTrackingTab : _tab;
        }

        /// <summary>Gets or sets whether Object Snap is shown; set true, it is.</summary>
        public bool IsObjectSnapTab
        {
            get => _tab == ObjectSnapTab;
            set => Tab = value ? ObjectSnapTab : _tab;
        }

        /// <summary>Gets the fourteen object snaps, in the order of AutoCAD's tab: Endpoint to Extension, then Insertion to Parallel.</summary>
        public IReadOnlyList<SnapModeRowViewModel> Modes { get; }

        /// <summary>Gets or sets "Object Snap On (F3)": the status bar's OSNAP.</summary>
        public bool IsSnapOn
        {
            get => _isSnapOn;
            set => Set(ref _isSnapOn, value);
        }

        /// <summary>Gets or sets "Snap On (F9)": the status bar's SNAP.</summary>
        public bool IsGridSnapOn
        {
            get => _isGridSnapOn;
            set => Set(ref _isGridSnapOn, value);
        }

        /// <summary>Gets or sets "Grid On (F7)": the status bar's GRID.</summary>
        public bool IsGridOn
        {
            get => _isGridOn;
            set => Set(ref _isGridOn, value);
        }

        /// <summary>Gets or sets "Polar Tracking On (F10)": the status bar's POLAR.</summary>
        public bool IsPolarOn
        {
            get => _isPolarOn;
            set => Set(ref _isPolarOn, value);
        }

        /// <summary>Gets or sets "Object Snap Tracking On (F11)": the status bar's OTRACK.</summary>
        public bool IsTrackingOn
        {
            get => _isTrackingOn;
            set => Set(ref _isTrackingOn, value);
        }

        /// <summary>Gets or sets the snap X spacing, as typed.</summary>
        public string SnapX
        {
            get => _snapX;
            set => Set(ref _snapX, value ?? string.Empty);
        }

        /// <summary>Gets or sets the snap Y spacing, as typed.</summary>
        public string SnapY
        {
            get => _snapY;
            set => Set(ref _snapY, value ?? string.Empty);
        }

        /// <summary>Gets the increment angles offered, in degrees, as AutoCAD's list: 90, 45, 30, 22.5, 18, 15, 10, 5.</summary>
        public IReadOnlyList<string> IncrementAngles => Increments;

        /// <summary>Gets or sets the polar increment angle in degrees, as typed or chosen.</summary>
        public string IncrementAngle
        {
            get => _increment;
            set => Set(ref _increment, value ?? string.Empty);
        }

        /// <summary>Gets a line at the foot: why Apply or OK did not take the settings; empty when they did.</summary>
        public string Notice
        {
            get => _notice;
            private set => Set(ref _notice, value ?? string.Empty);
        }

        /// <summary>Gets the command of Select All: every snap built ticked.</summary>
        public ICommand SelectAllCommand { get; }

        /// <summary>Gets the command of Clear All: every snap off.</summary>
        public ICommand ClearAllCommand { get; }

        /// <summary>Gets the command of Apply.</summary>
        public ICommand ApplyCommand { get; }

        /// <summary>Gets the command of OK.</summary>
        public ICommand OkCommand { get; }

        /// <summary>Gets the command of Cancel.</summary>
        public ICommand CancelCommand { get; }

        /// <summary>
        /// Gives the window the settings at once: the snaps ticked (<see cref="MainViewModel.SnapModes"/>), OSNAP, SNAP, GRID,
        /// POLAR and OTRACK, the snap spacing and the polar increment.
        /// </summary>
        /// <returns>Whether they were taken: false where a spacing or the increment is not a number above nought, said in <see cref="Notice"/>.</returns>
        public bool Apply()
        {
            if (!Positive(_snapX, out double x) || !Positive(_snapY, out double y))
            {
                Notice = "The snap spacing must be a number above nought.";
                return false;
            }

            if (!Positive(_increment, out double degrees) || degrees > 360.0)
            {
                Notice = "The increment angle must be a number above nought, 360 at most.";
                return false;
            }

            CadSnapModes modes = Modes.Where(r => r.IsChecked).Aggregate(CadSnapModes.None, (all, r) => all | r.Mode);
            _model.SnapModes = modes;
            _model.Toggles.First(t => t.Name == "OSNAP").IsOn = _isSnapOn;
            _model.Toggles.First(t => t.Name == "SNAP").IsOn = _isGridSnapOn;
            _model.Toggles.First(t => t.Name == "GRID").IsOn = _isGridOn;
            _model.Toggles.First(t => t.Name == "POLAR").IsOn = _isPolarOn;
            _model.Toggles.First(t => t.Name == "OTRACK").IsOn = _isTrackingOn;
            _model.SetSnapSpacing(x, y);
            _model.SetPolarAngle(degrees);
            Notice = string.Empty;
            return true;
        }

        /// <summary>Applies the settings and closes.</summary>
        /// <returns>Whether they were applied.</returns>
        public bool Ok()
        {
            if (!Apply())
            {
                return false;
            }

            CloseRequested?.Invoke(this, true);
            return true;
        }

        private void SetAll(bool on)
        {
            foreach (SnapModeRowViewModel row in Modes)
            {
                row.IsChecked = on;
            }
        }

        private static bool Positive(string text, out double value) =>
            double.TryParse((text ?? string.Empty).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            && value > 0.0
            && !double.IsInfinity(value);

        private static string Text(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);
    }
}
