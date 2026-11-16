using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>One box of the Ignore object property group of the Delete Duplicate Objects dialog: the property's name and whether OVERKILL leaves it out of its comparison.</summary>
    public sealed class OverkillIgnoreViewModel : ObservableObject
    {
        private bool _isChecked;

        internal OverkillIgnoreViewModel(string name, CadOverkillIgnore property, bool isChecked)
        {
            Name = name;
            Property = property;
            _isChecked = isChecked;
        }

        /// <summary>Gets the words beside the box: "Linetype scale".</summary>
        public string Name { get; }

        /// <summary>Gets the property the box stands for.</summary>
        public CadOverkillIgnore Property { get; }

        /// <summary>Gets or sets whether the property is ignored.</summary>
        public bool IsChecked
        {
            get => _isChecked;
            set => Set(ref _isChecked, value);
        }
    }

    /// <summary>
    /// The Delete Duplicate Objects dialog of OVERKILL, as AutoCAD 2021 shows it (design OverkillDialog): the tolerance, the
    /// nine properties that may be ignored in the comparison, and the options for polylines and for objects that overlap.
    /// The count of objects selected stands at the foot. A tolerance that is no number of nought or more is told at the foot
    /// and keeps OK from closing the dialog. OK gives the settings back through <see cref="Commit"/>.
    /// </summary>
    public sealed class OverkillViewModel : ObservableObject
    {
        private readonly CadOverkillRequest _request;
        private string _toleranceText;
        private bool _optimize;
        private bool _ignoreWidths;
        private bool _doNotBreak;
        private bool _partial;
        private bool _endToEnd;
        private bool _associativity;

        /// <summary>Makes the dialog over what OVERKILL hands the window.</summary>
        /// <param name="request">The request: the settings to show and how many objects are selected.</param>
        /// <exception cref="ArgumentNullException">Thrown when the request is null.</exception>
        public OverkillViewModel(CadOverkillRequest request)
        {
            _request = request ?? throw new ArgumentNullException(nameof(request));
            CadOverkillSettings settings = request.Settings;
            _toleranceText = settings.Tolerance.ToString("0.######", CultureInfo.InvariantCulture);
            _optimize = settings.OptimizePolylines;
            _ignoreWidths = settings.IgnorePolylineWidths;
            _doNotBreak = settings.DoNotBreakPolylines;
            _partial = settings.CombinePartialOverlaps;
            _endToEnd = settings.CombineEndToEnd;
            _associativity = settings.MaintainAssociativity;
            Ignored = new[]
            {
                Box("Color", CadOverkillIgnore.Color),
                Box("Layer", CadOverkillIgnore.Layer),
                Box("Linetype", CadOverkillIgnore.Linetype),
                Box("Linetype scale", CadOverkillIgnore.LinetypeScale),
                Box("Lineweight", CadOverkillIgnore.Lineweight),
                Box("Material", CadOverkillIgnore.Material),
                Box("Plot style", CadOverkillIgnore.PlotStyle),
                Box("Thickness", CadOverkillIgnore.Thickness),
                Box("Transparency", CadOverkillIgnore.Transparency),
            };

            OverkillIgnoreViewModel Box(string name, CadOverkillIgnore property) =>
                new OverkillIgnoreViewModel(name, property, (settings.Ignore & property) != 0);
        }

        /// <summary>Gets the boxes of the properties that may be ignored, in the dialog's order: Color, Layer, Linetype, Linetype scale, Lineweight, Material, Plot style, Thickness, Transparency.</summary>
        public IReadOnlyList<OverkillIgnoreViewModel> Ignored { get; }

        /// <summary>Gets or sets the tolerance as typed; the dialog shows it as 0.000001 where the settings are the default.</summary>
        public string ToleranceText
        {
            get => _toleranceText;
            set
            {
                if (Set(ref _toleranceText, value ?? string.Empty))
                {
                    Raise(nameof(IsValid));
                    Raise(nameof(Message));
                }
            }
        }

        /// <summary>Gets or sets whether the segments within polylines are optimized: compared with other objects, repeated vertices and collinear segments removed.</summary>
        public bool OptimizePolylines
        {
            get => _optimize;
            set
            {
                if (Set(ref _optimize, value))
                {
                    Raise(nameof(CanEditPolylineOptions));
                }
            }
        }

        /// <summary>Gets whether the two options that refine the optimizing of polylines can be set: only where the optimizing is on.</summary>
        public bool CanEditPolylineOptions => _optimize;

        /// <summary>Gets or sets whether the widths of polyline segments are left out of the comparison.</summary>
        public bool IgnorePolylineWidths
        {
            get => _ignoreWidths;
            set => Set(ref _ignoreWidths, value);
        }

        /// <summary>Gets or sets whether a polyline is kept whole: its segments that overlap another object are not cut out.</summary>
        public bool DoNotBreakPolylines
        {
            get => _doNotBreak;
            set => Set(ref _doNotBreak, value);
        }

        /// <summary>Gets or sets whether co-linear objects that overlap in part are combined.</summary>
        public bool CombinePartialOverlaps
        {
            get => _partial;
            set => Set(ref _partial, value);
        }

        /// <summary>Gets or sets whether co-linear objects that meet end to end are combined.</summary>
        public bool CombineEndToEnd
        {
            get => _endToEnd;
            set => Set(ref _endToEnd, value);
        }

        /// <summary>Gets or sets whether associative objects are maintained.</summary>
        public bool MaintainAssociativity
        {
            get => _associativity;
            set => Set(ref _associativity, value);
        }

        /// <summary>Gets the line at the foot: "12 objects selected".</summary>
        public string SelectedText =>
            _request.Selected.ToString(CultureInfo.InvariantCulture)
            + (_request.Selected == 1 ? " object selected" : " objects selected");

        /// <summary>Gets whether the tolerance typed is a number of nought or more, which OK needs.</summary>
        public bool IsValid => TryTolerance(out _);

        /// <summary>Gets what is wrong with the dialog, for the foot: the tolerance asked for a number; empty where all is well.</summary>
        public string Message => IsValid ? string.Empty : "The tolerance must be a number, nought or more.";

        /// <summary>Gives the settings the dialog holds to the request, as OK does, and sets its action to OK.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the tolerance is not valid.</exception>
        public void Commit()
        {
            if (!TryTolerance(out double tolerance))
            {
                throw new InvalidOperationException(Message);
            }

            CadOverkillSettings settings = _request.Settings;
            settings.Tolerance = tolerance;
            settings.Ignore = Ignored
                .Where(i => i.IsChecked)
                .Aggregate(CadOverkillIgnore.None, (all, one) => all | one.Property);
            settings.OptimizePolylines = _optimize;
            settings.IgnorePolylineWidths = _ignoreWidths;
            settings.DoNotBreakPolylines = _doNotBreak;
            settings.CombinePartialOverlaps = _partial;
            settings.CombineEndToEnd = _endToEnd;
            settings.MaintainAssociativity = _associativity;
            _request.Action = CadDialogAction.Ok;
        }

        private bool TryTolerance(out double value)
        {
            string text = (_toleranceText ?? string.Empty).Trim().Replace(',', '.');
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                && !double.IsInfinity(value)
                && value >= 0.0;
        }
    }
}
