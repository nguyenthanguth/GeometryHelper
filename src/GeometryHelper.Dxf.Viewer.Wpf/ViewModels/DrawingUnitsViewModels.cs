using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// AutoCAD's Drawing Units dialog, which UNITS shows, over the request its command hands the window: Length type and
    /// precision, Angle type, precision and Clockwise, Insertion scale, Sample Output following each change at once, Lighting
    /// greyed, and Direction… opening <see cref="DirectionControlViewModel"/>. OK writes the header as one undo step.
    /// </summary>
    public sealed class DrawingUnitsViewModel : ObservableObject
    {
        private static readonly string[] LengthNames =
        {
            "Scientific",
            "Decimal",
            "Engineering",
            "Architectural",
            "Fractional",
        };
        private static readonly string[] AngleNames =
        {
            "Decimal Degrees",
            "Deg/Min/Sec",
            "Grads",
            "Radians",
            "Surveyor's Units",
        };

        /// <summary>Makes the dialog over a request.</summary>
        /// <param name="request">The request of UNITS.</param>
        /// <exception cref="ArgumentNullException">Thrown when the request is null.</exception>
        public DrawingUnitsViewModel(CadUnitsRequest request)
        {
            Request = request ?? throw new ArgumentNullException(nameof(request));
            InsertionUnitsList = Enum.GetValues(typeof(DxfUnits)).Cast<DxfUnits>().OrderBy(u => (int)u).ToList();
        }

        /// <summary>Gets the request.</summary>
        public CadUnitsRequest Request { get; }

        /// <summary>Gets or sets how the window shows Direction Control; true where its pick closed it, so that this dialog closes too.</summary>
        public Func<DirectionControlViewModel, bool> ShowDirection { get; set; }

        /// <summary>Gets the length types, in the order of the board's list: Decimal, Architectural, Engineering, Fractional, Scientific.</summary>
        public IReadOnlyList<string> LengthTypes { get; } =
            new[] { "Decimal", "Architectural", "Engineering", "Fractional", "Scientific" };

        /// <summary>Gets the angle types: Decimal Degrees, Deg/Min/Sec, Grads, Radians, Surveyor's Units.</summary>
        public IReadOnlyList<string> AngleTypes => AngleNames;

        /// <summary>Gets the units of Insertion scale, Unitless first.</summary>
        public IReadOnlyList<DxfUnits> InsertionUnitsList { get; }

        /// <summary>Gets or sets the length type chosen (LUNITS).</summary>
        public string LengthType
        {
            get => LengthNames[Request.LengthUnits - 1];
            set
            {
                int index = Array.IndexOf(LengthNames, value);

                if (index >= 0 && index + 1 != Request.LengthUnits)
                {
                    Request.LengthUnits = index + 1;
                    Changed(nameof(LengthType));
                }
            }
        }

        /// <summary>Gets the precisions the length type offers, as AutoCAD's list shows them: 0.00 for two places, 0'-0 1/4" for a quarter.</summary>
        public IReadOnlyList<string> LengthPrecisions => Enumerable.Range(0, 9).Select(Precision).ToList();

        /// <summary>Gets or sets the length precision chosen (LUPREC), as its list shows it.</summary>
        public string LengthPrecision
        {
            get => Precision(Request.LengthPrecision);
            set
            {
                int index = LengthPrecisions.ToList().IndexOf(value);

                if (index >= 0 && index != Request.LengthPrecision)
                {
                    Request.LengthPrecision = index;
                    Changed(nameof(LengthPrecision));
                }
            }
        }

        /// <summary>Gets or sets the angle type chosen (AUNITS).</summary>
        public string AngleType
        {
            get => AngleNames[Request.AngleUnits];
            set
            {
                int index = Array.IndexOf(AngleNames, value);

                if (index >= 0 && index != Request.AngleUnits)
                {
                    Request.AngleUnits = index;
                    Changed(nameof(AngleType));
                }
            }
        }

        /// <summary>Gets the precisions the angle type offers, as AutoCAD's list shows them: 0, 0.0 and on, or 0d, 0d00' and on.</summary>
        public IReadOnlyList<string> AnglePrecisions => Enumerable.Range(0, 9).Select(AnglePrecisionText).ToList();

        /// <summary>Gets or sets the angle precision chosen (AUPREC), as its list shows it.</summary>
        public string AnglePrecision
        {
            get => AnglePrecisionText(Request.AnglePrecision);
            set
            {
                int index = AnglePrecisions.ToList().IndexOf(value);

                if (index >= 0 && index != Request.AnglePrecision)
                {
                    Request.AnglePrecision = index;
                    Changed(nameof(AnglePrecision));
                }
            }
        }

        /// <summary>Gets or sets whether angles grow clockwise (ANGDIR).</summary>
        public bool Clockwise
        {
            get => Request.Clockwise;
            set
            {
                Request.Clockwise = value;
                Changed(nameof(Clockwise));
            }
        }

        /// <summary>Gets or sets the units of Insertion scale (INSUNITS).</summary>
        public DxfUnits InsertionUnits
        {
            get => Request.InsertionUnits;
            set
            {
                Request.InsertionUnits = value;
                Changed(nameof(InsertionUnits));
            }
        }

        /// <summary>Gets the first line of Sample Output.</summary>
        public string SampleLength => Request.SampleLength;

        /// <summary>Gets the second line of Sample Output.</summary>
        public string SampleAngle => Request.SampleAngle;

        /// <summary>Direction…: Direction Control over the request; its pick closes both dialogs for the command line.</summary>
        /// <returns>Whether the dialog is to close for the pick.</returns>
        public bool Direction()
        {
            if (ShowDirection == null)
            {
                return false;
            }

            Request.IsDirectionOpen = true;
            bool picked = ShowDirection(new DirectionControlViewModel(Request));
            Request.IsDirectionOpen = picked;
            return picked;
        }

        /// <summary>Closes the dialog with an action: OK writes the header, Cancel leaves it.</summary>
        /// <param name="action">OK or Cancel.</param>
        public void Close(CadDialogAction action) => Request.Action = action;

        private string Precision(int places)
        {
            switch (Request.LengthUnits)
            {
                case 3:
                    return "0'-0" + (places == 0 ? string.Empty : "." + new string('0', places)) + "\"";
                case 4:
                    return places == 0
                        ? "0'-0\""
                        : "0'-0 1/" + (1 << places).ToString(CultureInfo.InvariantCulture) + "\"";
                case 5:
                    return places == 0 ? "0" : "0 1/" + (1 << places).ToString(CultureInfo.InvariantCulture);
                case 1:
                    return (places == 0 ? "0" : "0." + new string('0', places)) + "E+01";
                default:
                    return places == 0 ? "0" : "0." + new string('0', places);
            }
        }

        private string AnglePrecisionText(int places)
        {
            switch (Request.AngleUnits)
            {
                case 1:
                case 4:
                    string dms =
                        places == 0 ? "0d"
                        : places <= 2 ? "0d" + new string('0', places) + "'"
                        : "0d00'"
                            + new string('0', Math.Min(2, places - 2))
                            + (places > 4 ? "." + new string('0', places - 4) : string.Empty)
                            + "\"";
                    return Request.AngleUnits == 4 ? "N " + dms + " E" : dms;
                case 2:
                    return (places == 0 ? "0" : "0." + new string('0', places)) + "g";
                case 3:
                    return (places == 0 ? "0" : "0." + new string('0', places)) + "r";
                default:
                    return places == 0 ? "0" : "0." + new string('0', places);
            }
        }

        private void Changed(string name)
        {
            Raise(name);
            Raise(nameof(LengthPrecisions));
            Raise(nameof(LengthPrecision));
            Raise(nameof(AnglePrecisions));
            Raise(nameof(AnglePrecision));
            Raise(nameof(SampleLength));
            Raise(nameof(SampleAngle));
        }
    }

    /// <summary>
    /// AutoCAD's Direction Control of Drawing Units: Base Angle East 0, North 90, West 180, South 270 or Other, whose angle
    /// is typed or picked; the pick closes the dialogs for the command line to ask two points (ANGBASE).
    /// </summary>
    public sealed class DirectionControlViewModel : ObservableObject
    {
        private readonly double _was;
        private string _angle;
        private bool _other;

        /// <summary>Makes the dialog over the request of UNITS.</summary>
        /// <param name="request">The request.</param>
        /// <exception cref="ArgumentNullException">Thrown when the request is null.</exception>
        public DirectionControlViewModel(CadUnitsRequest request)
        {
            Request = request ?? throw new ArgumentNullException(nameof(request));
            _was = request.BaseAngle;
            double degrees = request.BaseAngle * 180.0 / Math.PI;
            _other = !new[] { 0.0, 90.0, 180.0, 270.0 }.Any(d => Math.Abs(d - degrees) < 1E-9);
            _angle = CadUnits.Angle(request.BaseAngle, request.AngleUnits, request.AnglePrecision);
        }

        /// <summary>Gets the request.</summary>
        public CadUnitsRequest Request { get; }

        /// <summary>Gets or sets whether East (0) is chosen.</summary>
        public bool East
        {
            get => Is(0.0);
            set => Choose(value, 0.0);
        }

        /// <summary>Gets or sets whether North (90) is chosen.</summary>
        public bool North
        {
            get => Is(90.0);
            set => Choose(value, 90.0);
        }

        /// <summary>Gets or sets whether West (180) is chosen.</summary>
        public bool West
        {
            get => Is(180.0);
            set => Choose(value, 180.0);
        }

        /// <summary>Gets or sets whether South (270) is chosen.</summary>
        public bool South
        {
            get => Is(270.0);
            set => Choose(value, 270.0);
        }

        /// <summary>Gets or sets whether Other is chosen, which opens Angle and its pick.</summary>
        public bool Other
        {
            get => _other;
            set
            {
                if (value && !_other)
                {
                    _other = true;
                    RaiseAll();
                }
            }
        }

        /// <summary>Gets or sets the angle of Other as typed, in the angle units of the dialog.</summary>
        public string Angle
        {
            get => _angle;
            set => Set(ref _angle, value ?? string.Empty);
        }

        /// <summary>Pick an angle: the dialogs closed for the command line to ask two points.</summary>
        public void Pick() => Request.Action = CadDialogAction.SelectObjects;

        /// <summary>OK: the base angle taken, Other's typed angle read in the angle units.</summary>
        /// <returns>Whether it read.</returns>
        public bool Ok()
        {
            if (!_other)
            {
                return true;
            }

            if (!CadUnits.TryAngle(_angle, Request.AngleUnits, out double radians))
            {
                return false;
            }

            double turn = radians % (2.0 * Math.PI);
            Request.BaseAngle = turn < 0.0 ? turn + 2.0 * Math.PI : turn;
            return true;
        }

        /// <summary>Cancel: the base angle the dialog was opened with.</summary>
        public void Cancel() => Request.BaseAngle = _was;

        private bool Is(double degrees) => !_other && Math.Abs(Request.BaseAngle * 180.0 / Math.PI - degrees) < 1E-9;

        private void Choose(bool value, double degrees)
        {
            if (!value)
            {
                return;
            }

            _other = false;
            Request.BaseAngle = degrees * Math.PI / 180.0;
            RaiseAll();
        }

        private void RaiseAll()
        {
            Raise(nameof(East));
            Raise(nameof(North));
            Raise(nameof(West));
            Raise(nameof(South));
            Raise(nameof(Other));
        }
    }
}
