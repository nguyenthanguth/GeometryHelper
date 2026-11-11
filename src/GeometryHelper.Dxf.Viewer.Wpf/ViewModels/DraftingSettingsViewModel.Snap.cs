using System;
using System.Globalization;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The Snap and Grid tab of Drafting Settings past Snap On and its spacing, as AutoCAD 2021's tab: Equal X and Y spacing,
    /// Polar distance (POLARDIST), Grid snap or PolarSnap (SNAPTYPE), Rectangular or Isometric snap (SNAPSTYL), Grid On, the
    /// dotted grid of Grid style (GRIDSTYLE), the grid spacing (GRIDUNIT), Major line every (GRIDMAJOR) and Grid behavior
    /// (GRIDDISPLAY). With the isometric snap the Y boxes show the full vertical spacing, twice what the drawing holds, and
    /// the X boxes are greyed: a snap of 8.6603 by 2.5 shows 5. A greyed box is neither checked nor written; Apply writes
    /// the values changed, as typing them does.
    /// </summary>
    public sealed partial class DraftingSettingsViewModel
    {
        /// <summary>The name <see cref="FaultyBox"/> gives Snap X spacing.</summary>
        public const string SnapXBox = "SnapX";

        /// <summary>The name <see cref="FaultyBox"/> gives Snap Y spacing.</summary>
        public const string SnapYBox = "SnapY";

        /// <summary>The name <see cref="FaultyBox"/> gives Polar distance.</summary>
        public const string PolarDistanceBox = "PolarDistance";

        /// <summary>The name <see cref="FaultyBox"/> gives Grid X spacing.</summary>
        public const string GridXBox = "GridX";

        /// <summary>The name <see cref="FaultyBox"/> gives Grid Y spacing.</summary>
        public const string GridYBox = "GridY";

        /// <summary>The name <see cref="FaultyBox"/> gives Major line every.</summary>
        public const string MajorLineEveryBox = "MajorLineEvery";

        private bool _isEqualSpacing;
        private string _polarDistance;
        private bool _isPolarSnapType;
        private bool _isIsometricSnap;
        private string _gridX;
        private string _gridY;
        private string _majorLineEvery;
        private int _gridStyle;
        private int _gridDisplay;
        private string _faultyBox = string.Empty;

        // The values as the dialog opened, so that Apply writes only those changed.
        private string _loadedSnapX;
        private string _loadedSnapY;
        private string _loadedPolarDistance;
        private bool _loadedPolarSnap;
        private bool _loadedIsometric;
        private string _loadedGridX;
        private string _loadedGridY;
        private int _loadedGridMajor;
        private int _loadedGridStyle;
        private int _loadedGridDisplay;

        /// <summary>
        /// Gets or sets "Equal X and Y spacing": while ticked, a spacing typed in an X box is copied to its Y box and back, for
        /// the snap and the grid; ticked, each Y box takes its X box's. It is the dialog's own, ticked as it opens where the
        /// snap and the grid are each as wide as they are high.
        /// </summary>
        public bool IsEqualSpacing
        {
            get => _isEqualSpacing;
            set
            {
                if (Set(ref _isEqualSpacing, value) && value && CanEqualSpacing)
                {
                    SnapY = _snapX;
                    GridY = _gridX;
                }
            }
        }

        /// <summary>Gets or sets the step of PolarSnap, as typed (POLARDIST): nought or above, nought taking the snap X spacing.</summary>
        public string PolarDistance
        {
            get => _polarDistance;
            set
            {
                if (Set(ref _polarDistance, value ?? string.Empty))
                {
                    ClearFault(PolarDistanceBox);
                }
            }
        }

        /// <summary>Gets or sets "PolarSnap" (SNAPTYPE 1); false is "Grid snap".</summary>
        public bool IsPolarSnapType
        {
            get => _isPolarSnapType;
            set
            {
                if (Set(ref _isPolarSnapType, value))
                {
                    Raise(nameof(IsGridSnapType));
                    RaiseSnapState();
                }
            }
        }

        /// <summary>Gets or sets "Grid snap" (SNAPTYPE 0), the other choice of <see cref="IsPolarSnapType"/>.</summary>
        public bool IsGridSnapType
        {
            get => !_isPolarSnapType;
            set => IsPolarSnapType = !value;
        }

        /// <summary>Gets or sets "Isometric snap" (SNAPSTYL 1); false is "Rectangular snap". It keeps its choice while greyed.</summary>
        public bool IsIsometricSnap
        {
            get => _isIsometricSnap;
            set
            {
                if (Set(ref _isIsometricSnap, value))
                {
                    Raise(nameof(IsRectangularSnap));
                    RaiseSnapState();
                }
            }
        }

        /// <summary>Gets or sets "Rectangular snap" (SNAPSTYL 0), the other choice of <see cref="IsIsometricSnap"/>.</summary>
        public bool IsRectangularSnap
        {
            get => !_isIsometricSnap;
            set => IsIsometricSnap = !value;
        }

        /// <summary>Gets whether Snap X spacing takes a value: not with the isometric snap, nor with PolarSnap.</summary>
        public bool CanEditSnapX => !_isIsometricSnap && !_isPolarSnapType;

        /// <summary>Gets whether Snap Y spacing takes a value: not with PolarSnap.</summary>
        public bool CanEditSnapY => !_isPolarSnapType;

        /// <summary>Gets whether Equal X and Y spacing can be ticked: not with the isometric snap, nor with PolarSnap.</summary>
        public bool CanEqualSpacing => !_isIsometricSnap && !_isPolarSnapType;

        /// <summary>Gets whether Polar distance takes a value: with PolarSnap alone.</summary>
        public bool CanEditPolarDistance => _isPolarSnapType;

        /// <summary>Gets whether Rectangular and Isometric snap can be chosen: with Grid snap alone.</summary>
        public bool CanChooseSnapStyle => !_isPolarSnapType;

        /// <summary>Gets whether Grid X spacing takes a value: not with the isometric snap.</summary>
        public bool CanEditGridX => !_isIsometricSnap;

        /// <summary>Gets or sets the grid X spacing, as typed (GRIDUNIT): nought takes the snap X spacing.</summary>
        public string GridX
        {
            get => _gridX;
            set
            {
                if (Set(ref _gridX, value ?? string.Empty))
                {
                    ClearFault(GridXBox);

                    if (_isEqualSpacing && CanEqualSpacing)
                    {
                        GridY = _gridX;
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the grid Y spacing, as typed (GRIDUNIT): nought takes the snap Y spacing. With the isometric snap it is
        /// the full vertical spacing, twice what the drawing holds.
        /// </summary>
        public string GridY
        {
            get => _gridY;
            set
            {
                if (Set(ref _gridY, value ?? string.Empty))
                {
                    ClearFault(GridYBox);

                    if (_isEqualSpacing && CanEqualSpacing)
                    {
                        GridX = _gridY;
                    }
                }
            }
        }

        /// <summary>Gets or sets "Major line every", as typed (GRIDMAJOR): a whole number from 1 to 100.</summary>
        public string MajorLineEvery
        {
            get => _majorLineEvery;
            set
            {
                if (Set(ref _majorLineEvery, value ?? string.Empty))
                {
                    ClearFault(MajorLineEveryBox);
                }
            }
        }

        /// <summary>Gets or sets "2D model space" of "Display dotted grid in" (GRIDSTYLE 1).</summary>
        public bool IsDottedInModel
        {
            get => (_gridStyle & 1) != 0;
            set => SetGridStyleBit(1, value, nameof(IsDottedInModel));
        }

        /// <summary>Gets or sets "Block editor" of "Display dotted grid in" (GRIDSTYLE 2).</summary>
        public bool IsDottedInBlockEditor
        {
            get => (_gridStyle & 2) != 0;
            set => SetGridStyleBit(2, value, nameof(IsDottedInBlockEditor));
        }

        /// <summary>Gets or sets "Sheet/Layout" of "Display dotted grid in" (GRIDSTYLE 4).</summary>
        public bool IsDottedInLayout
        {
            get => (_gridStyle & 4) != 0;
            set => SetGridStyleBit(4, value, nameof(IsDottedInLayout));
        }

        /// <summary>Gets or sets "Adaptive grid" (GRIDDISPLAY 2).</summary>
        public bool IsAdaptiveGrid
        {
            get => (_gridDisplay & 2) != 0;
            set
            {
                if (SetGridDisplayBit(2, value, nameof(IsAdaptiveGrid)))
                {
                    Raise(nameof(CanSubdivide));
                }
            }
        }

        /// <summary>
        /// Gets or sets "Allow subdivision below grid spacing" (GRIDDISPLAY 4); kept while greyed, as AutoCAD ignores it
        /// without an adaptive grid.
        /// </summary>
        public bool IsSubdividing
        {
            get => (_gridDisplay & 4) != 0;
            set => SetGridDisplayBit(4, value, nameof(IsSubdividing));
        }

        /// <summary>Gets whether "Allow subdivision below grid spacing" can be ticked: with an adaptive grid alone.</summary>
        public bool CanSubdivide => (_gridDisplay & 2) != 0;

        /// <summary>Gets or sets "Display grid beyond Limits" (GRIDDISPLAY 1).</summary>
        public bool IsBeyondLimits
        {
            get => (_gridDisplay & 1) != 0;
            set => SetGridDisplayBit(1, value, nameof(IsBeyondLimits));
        }

        /// <summary>Gets or sets "Follow Dynamic UCS" (GRIDDISPLAY 8): kept and written, with no effect in a 2D drawing.</summary>
        public bool IsFollowingUcs
        {
            get => (_gridDisplay & 8) != 0;
            set => SetGridDisplayBit(8, value, nameof(IsFollowingUcs));
        }

        /// <summary>
        /// Gets the box Apply or OK refused, edged in red with the caret put in it: <see cref="SnapXBox"/> and the other names;
        /// empty while none is. Typing in it clears it, and the notice with it.
        /// </summary>
        public string FaultyBox
        {
            get => _faultyBox;
            private set => Set(ref _faultyBox, value ?? string.Empty);
        }

        /// <summary>The tab's state read from the drawing shown and the window's settings.</summary>
        private void LoadSnap()
        {
            GlobalSettings settings = _model.Settings;
            GeoVector2 grid = _model.GridUnit;
            _isIsometricSnap = _model.Isoplane.HasValue;
            _isPolarSnapType = settings.PolarSnap;
            _snapX = Text(_model.Snap.GridX);
            _snapY = Text(_isIsometricSnap ? 2.0 * _model.Snap.GridY : _model.Snap.GridY);
            _polarDistance = Text(settings.PolarDistance);
            _gridX = Text(grid.X);
            _gridY = Text(_isIsometricSnap ? 2.0 * grid.Y : grid.Y);
            _majorLineEvery = _model.GridMajor.ToString(CultureInfo.InvariantCulture);
            _gridStyle = settings.GridStyle;
            _gridDisplay = _model.GridDisplay;
            _isEqualSpacing = !_isIsometricSnap && _snapX == _snapY && _gridX == _gridY;

            _loadedSnapX = _snapX;
            _loadedSnapY = _snapY;
            _loadedPolarDistance = _polarDistance;
            _loadedPolarSnap = _isPolarSnapType;
            _loadedIsometric = _isIsometricSnap;
            _loadedGridX = _gridX;
            _loadedGridY = _gridY;
            _loadedGridMajor = _model.GridMajor;
            _loadedGridStyle = _gridStyle;
            _loadedGridDisplay = _gridDisplay;
        }

        /// <summary>
        /// Checks the boxes of the tab that take a value, in the order of the tab: the snap spacing above nought, Polar
        /// distance nought or above, the grid spacing a number (one below nought is taken as its size, as GRIDUNIT takes it),
        /// Major line every a whole number from 1 to 100. The first refused is said in <see cref="Notice"/> and named in
        /// <see cref="FaultyBox"/>.
        /// </summary>
        private bool CheckSnap(out SnapValues values)
        {
            values = new SnapValues();

            if (CanEditSnapX && !Positive(_snapX, out values.SnapX))
            {
                return Refuse(SnapXBox, "The snap spacing must be a number above nought.");
            }

            if (CanEditSnapY && !Positive(_snapY, out values.SnapY))
            {
                return Refuse(SnapYBox, "The snap spacing must be a number above nought.");
            }

            if (
                CanEditPolarDistance
                && (!Number(_polarDistance, out values.PolarDistance) || values.PolarDistance < 0.0)
            )
            {
                return Refuse(PolarDistanceBox, "The polar distance must be a number, nought or above.");
            }

            if (CanEditGridX && !Number(_gridX, out values.GridX))
            {
                return Refuse(GridXBox, "The grid spacing must be a number, nought or above.");
            }

            if (!Number(_gridY, out values.GridY))
            {
                return Refuse(GridYBox, "The grid spacing must be a number, nought or above.");
            }

            if (
                !int.TryParse(
                    (_majorLineEvery ?? string.Empty).Trim(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out values.GridMajor
                )
                || values.GridMajor < 1
                || values.GridMajor > 100
            )
            {
                return Refuse(MajorLineEveryBox, "Requires an integer between 1 and 100.");
            }

            FaultyBox = string.Empty;
            return true;
        }

        /// <summary>
        /// Writes the values of the tab changed since the dialog opened: SNAPSTYL, SNAPUNIT, SNAPTYPE, POLARDIST, GRIDUNIT,
        /// GRIDMAJOR, GRIDSTYLE and GRIDDISPLAY. With the isometric snap a vertical spacing of 5 is written as SNAP Style
        /// Isometric writes it, 8.6603 by 2.5, and the grid's the same way, nought kept nought.
        /// </summary>
        private void ApplySnap(SnapValues values)
        {
            bool styleChanged = _isIsometricSnap != _loadedIsometric;

            if (styleChanged)
            {
                _model.SetIsometricSnap(_isIsometricSnap);
            }

            if (CanEditSnapY && (styleChanged || _snapX != _loadedSnapX || _snapY != _loadedSnapY))
            {
                if (_isIsometricSnap)
                {
                    _model.SetSnapSpacing(values.SnapY * Math.Sqrt(3.0), values.SnapY / 2.0);
                }
                else
                {
                    _model.SetSnapSpacing(values.SnapX, values.SnapY);
                }
            }

            if (_isPolarSnapType != _loadedPolarSnap)
            {
                _model.SetPolarSnap(_isPolarSnapType);
            }

            if (CanEditPolarDistance && _polarDistance != _loadedPolarDistance)
            {
                _model.SetPolarDistance(values.PolarDistance);
            }

            if (styleChanged || _gridX != _loadedGridX || _gridY != _loadedGridY)
            {
                double up = Math.Abs(values.GridY);

                if (_isIsometricSnap)
                {
                    _model.SetGridSpacing(up * Math.Sqrt(3.0), up / 2.0);
                }
                else
                {
                    _model.SetGridSpacing(Math.Abs(values.GridX), up);
                }
            }

            if (values.GridMajor != _loadedGridMajor)
            {
                _model.SetGridMajor(values.GridMajor);
            }

            if (_gridStyle != _loadedGridStyle)
            {
                _model.SetGridStyle(_gridStyle);
            }

            if (_gridDisplay != _loadedGridDisplay)
            {
                _model.SetGridDisplay(_gridDisplay);
            }

            // The grid boxes show what was written, a spacing typed below nought as its size; the next Apply writes what
            // changes from here.
            GeoVector2 grid = _model.GridUnit;
            _gridX = Text(grid.X);
            _gridY = Text(_isIsometricSnap ? 2.0 * grid.Y : grid.Y);
            Raise(nameof(GridX));
            Raise(nameof(GridY));
            _loadedSnapX = _snapX;
            _loadedSnapY = _snapY;
            _loadedPolarDistance = _polarDistance;
            _loadedPolarSnap = _isPolarSnapType;
            _loadedIsometric = _isIsometricSnap;
            _loadedGridX = _gridX;
            _loadedGridY = _gridY;
            _loadedGridMajor = values.GridMajor;
            _loadedGridStyle = _gridStyle;
            _loadedGridDisplay = _gridDisplay;
        }

        private bool Refuse(string box, string notice)
        {
            Notice = notice;
            FaultyBox = box;
            return false;
        }

        /// <summary>A box at fault typed in: the fault and the notice cleared.</summary>
        private void ClearFault(string box)
        {
            if (_faultyBox == box)
            {
                FaultyBox = string.Empty;
                Notice = string.Empty;
            }
        }

        private void SetGridStyleBit(int bit, bool on, string name)
        {
            int bits = on ? _gridStyle | bit : _gridStyle & ~bit;

            if (bits != _gridStyle)
            {
                _gridStyle = bits;
                Raise(name);
            }
        }

        private bool SetGridDisplayBit(int bit, bool on, string name)
        {
            int bits = on ? _gridDisplay | bit : _gridDisplay & ~bit;

            if (bits == _gridDisplay)
            {
                return false;
            }

            _gridDisplay = bits;
            Raise(name);
            return true;
        }

        private void RaiseSnapState()
        {
            Raise(nameof(CanEditSnapX));
            Raise(nameof(CanEditSnapY));
            Raise(nameof(CanEqualSpacing));
            Raise(nameof(CanEditPolarDistance));
            Raise(nameof(CanChooseSnapStyle));
            Raise(nameof(CanEditGridX));
        }

        private static bool Number(string text, out double value) =>
            double.TryParse((text ?? string.Empty).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            && !double.IsInfinity(value)
            && !double.IsNaN(value);

        /// <summary>The values of the tab read from its boxes; those of a greyed box left nought.</summary>
        private struct SnapValues
        {
            public double SnapX;
            public double SnapY;
            public double PolarDistance;
            public double GridX;
            public double GridY;
            public int GridMajor;
        }
    }
}
