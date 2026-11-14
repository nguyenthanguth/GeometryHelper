using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>What a system variable holds.</summary>
    public enum CadVariableKind
    {
        /// <summary>A whole number, as OSMODE's 4133.</summary>
        Integer,

        /// <summary>A real number, as LTSCALE's 1.0000.</summary>
        Real,

        /// <summary>A text, as CLAYER's "0".</summary>
        Text,

        /// <summary>A point, as INSBASE's 0.0000,0.0000,0.0000.</summary>
        Point,
    }

    /// <summary>
    /// A system variable, as AutoCAD's: a name SETVAR asks, or typed alone, and the value it reads and sets where the presenter
    /// keeps it. One kept in the drawing (LTSCALE, $LTSCALE of the header) is changed as one step of undo, as AutoCAD records
    /// a change of the header; one of the app (OSMODE, PICKBOX) is changed at once, with no step. "Enter new value for OSMODE
    /// &lt;4133&gt;:" asks a whole number, 32 768 refused with "Requires an integer between 0 and 32767." and asked again;
    /// Enter keeps the value. A variable read only says its value, "EXTMIN = 0.0000,0.0000,0.0000 (read only)".
    /// </summary>
    public sealed class CadSystemVariable
    {
        private readonly Func<CadCommandSession, string> _show;
        private Func<CadCommandContext, IEnumerable<CadPrompt>> _ask;
        private CadEditRecordKind _recordKind = CadEditRecordKind.SettingChanged;
        private Func<CadCommandSession, double[]> _numbers;

        private CadSystemVariable(
            string name,
            CadVariableKind kind,
            bool inDrawing,
            Func<CadCommandSession, string> show
        )
        {
            if (string.IsNullOrWhiteSpace(name) || name.Trim().IndexOf(' ') >= 0)
            {
                throw new ArgumentException("A variable's name is one word: " + name + ".", nameof(name));
            }

            Name = name.Trim().ToUpperInvariant();
            Kind = kind;
            IsInDrawing = inDrawing;
            _show = show;
        }

        /// <summary>Gets the name, in capitals: OSMODE.</summary>
        public string Name { get; }

        /// <summary>Gets what it holds.</summary>
        public CadVariableKind Kind { get; }

        /// <summary>Gets whether it is kept in the drawing, so that a change of it is a step of undo and is saved with the drawing.</summary>
        public bool IsInDrawing { get; }

        /// <summary>Gets whether it can only be read, as EXTMIN and DWGNAME.</summary>
        public bool IsReadOnly => _ask == null;

        /// <summary>
        /// The prompts the variable asks when its name is typed alone, where they are not SETVAR's own: CECOLOR asks
        /// "Enter new object color [Truecolor/COlorbook] &lt;BYLAYER&gt;:"; null for SETVAR's.
        /// </summary>
        internal Func<CadCommandContext, IEnumerable<CadPrompt>> Own { get; private set; }

        /// <summary>
        /// Gets the value as SETVAR shows it in its angle brackets: a whole number as 4133, a real to four places as 1.0000 (an
        /// angle as 90), a text in quotes as "0", a point as 0.0000,0.0000,0.0000.
        /// </summary>
        /// <param name="session">The command line of the document whose value it is.</param>
        /// <returns>The text.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the session is null.</exception>
        public string GetValue(CadCommandSession session) =>
            _show(session ?? throw new ArgumentNullException(nameof(session)));

        /// <summary>
        /// Makes a whole number the app keeps, as OSMODE: asked "Enter new value for OSMODE &lt;4133&gt;:", a number outside
        /// <paramref name="least"/> to <paramref name="most"/> refused with AutoCAD's "Requires an integer between 0 and 32767."
        /// and asked again; set at once, no step of undo.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="least">The least value taken.</param>
        /// <param name="most">The greatest value taken.</param>
        /// <param name="get">Reads the value.</param>
        /// <param name="set">Sets a value taken.</param>
        /// <returns>The variable.</returns>
        /// <exception cref="ArgumentNullException">Thrown when either delegate is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the name is empty or has a space, or the least value is above the greatest.</exception>
        public static CadSystemVariable Integer(string name, int least, int most, Func<int> get, Action<int> set)
        {
            Func<int> read = get ?? throw new ArgumentNullException(nameof(get));
            Action<int> write = set ?? throw new ArgumentNullException(nameof(set));
            return Whole(name, false, least, most, s => read(), (c, v) => write(v));
        }

        /// <summary>
        /// Makes a real number above nought the app keeps, as POLARANG: asked "Enter new value for POLARANG &lt;90&gt;:", shown
        /// to as few places as it needs; nought or less refused with "Value must be positive and nonzero.", one above
        /// <paramref name="most"/> with "Value must be at most 360.", and asked again; set at once, no step of undo.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="most">The greatest value taken, 360 for an angle in degrees.</param>
        /// <param name="get">Reads the value.</param>
        /// <param name="set">Sets a value taken.</param>
        /// <returns>The variable.</returns>
        /// <exception cref="ArgumentNullException">Thrown when either delegate is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the name is empty or has a space.</exception>
        public static CadSystemVariable Real(string name, double most, Func<double> get, Action<double> set)
        {
            Func<double> read = get ?? throw new ArgumentNullException(nameof(get));
            Action<double> write = set ?? throw new ArgumentNullException(nameof(set));
            return Number(name, false, RealBound.Positive, true, s => read(), (c, v) => write(v), most: most);
        }

        /// <summary>
        /// Makes a text the app keeps that may be empty, as POLARADDANG: asked "Enter new value for POLARADDANG, or . for none
        /// &lt;""&gt;:", a period for an empty text; one refused written off with AutoCAD's "Cannot set POLARADDANG to that
        /// value." and not asked again; set at once, no step of undo.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="get">Reads the value.</param>
        /// <param name="take">Sets a text typed, empty for the period, and gives whether it was taken.</param>
        /// <returns>The variable.</returns>
        /// <exception cref="ArgumentNullException">Thrown when either delegate is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the name is empty or has a space.</exception>
        public static CadSystemVariable Text(string name, Func<string> get, Func<string, bool> take)
        {
            Func<string> read = get ?? throw new ArgumentNullException(nameof(get));
            Func<string, bool> write = take ?? throw new ArgumentNullException(nameof(take));
            var variable = new CadSystemVariable(name, CadVariableKind.Text, false, s => "\"" + read() + "\"");
            variable._ask = c => AskOptionalText(c, variable, write);
            return variable;
        }

        /// <summary>
        /// Makes an angle the app keeps, as SNAPANG: asked "Enter new value for SNAPANG &lt;30&gt;:" in the drawing's angle
        /// units (AUNITS, AUPREC), typed in them or given by two points, and kept from nought up to a whole turn, as AutoCAD
        /// keeps 400 as 40 and -30 as 330; set at once, no step of undo.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="get">Reads the value, in radians.</param>
        /// <param name="set">Sets a value taken, in radians from nought up to a whole turn.</param>
        /// <returns>The variable.</returns>
        /// <exception cref="ArgumentNullException">Thrown when either delegate is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the name is empty or has a space.</exception>
        public static CadSystemVariable Angle(string name, Func<double> get, Action<double> set)
        {
            Func<double> read = get ?? throw new ArgumentNullException(nameof(get));
            Action<double> write = set ?? throw new ArgumentNullException(nameof(set));
            var variable = new CadSystemVariable(name, CadVariableKind.Real, false, s => ShowAngle(s, read()));
            variable._ask = c => AskAngle(c, variable, read, write);
            variable._numbers = s => new[] { read() };
            return variable;
        }

        /// <summary>
        /// Makes a point the app keeps, as SNAPBASE: asked "Enter new value for SNAPBASE &lt;0.0000,0.0000&gt;:", typed or
        /// picked, a number alone the point that far from the last point towards the cursor, as AutoCAD 2021 takes SNAPBASE
        /// 5; set at once, no step of undo.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="get">Reads the value.</param>
        /// <param name="set">Sets a point taken.</param>
        /// <returns>The variable.</returns>
        /// <exception cref="ArgumentNullException">Thrown when either delegate is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the name is empty or has a space.</exception>
        public static CadSystemVariable Point(string name, Func<GeoPoint2> get, Action<GeoPoint2> set)
        {
            Func<GeoPoint2> read = get ?? throw new ArgumentNullException(nameof(get));
            Action<GeoPoint2> write = set ?? throw new ArgumentNullException(nameof(set));
            return Point(name, false, false, s => new GeoPoint3(read().X, read().Y, 0.0), (c, p) => write(p), null);
        }

        /// <summary>
        /// Makes a point the app keeps that refuses some values, as SNAPUNIT: asked "Enter new value for SNAPUNIT
        /// &lt;10.0000,10.0000&gt;:", typed or picked, a number alone the point that far from the last point towards the cursor
        /// (GRIDUNIT 5 with the last point at (10, 20) and the cursor at (13, 24) is 13,24, as AutoCAD 2021 takes it), text
        /// that is no point refused with "Invalid 2D point." and asked again; a
        /// point the take refuses, as 0,5 for a snap spacing, written off with AutoCAD's "Cannot set SNAPUNIT to that value."
        /// and not asked again; set at once, no step of undo.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="get">Reads the value.</param>
        /// <param name="take">Sets a point typed or picked, and gives whether it was taken.</param>
        /// <returns>The variable.</returns>
        /// <exception cref="ArgumentNullException">Thrown when either delegate is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the name is empty or has a space.</exception>
        public static CadSystemVariable Point(string name, Func<GeoPoint2> get, Func<GeoPoint2, bool> take)
        {
            Func<GeoPoint2> read = get ?? throw new ArgumentNullException(nameof(get));
            Func<GeoPoint2, bool> write = take ?? throw new ArgumentNullException(nameof(take));
            var variable = new CadSystemVariable(name, CadVariableKind.Point, false, s => Show(P3(read()), false));
            variable._ask = c => AskPointTaken(c, variable, read, write);
            variable._numbers = s => new[] { read().X, read().Y, 0.0 };
            return variable;
        }

        /// <summary>
        /// Makes a distance the app keeps, of either sign, as POLARDIST: asked "Enter new value for POLARDIST &lt;0.0000&gt;:",
        /// shown to four places, typed or given by two points, 0,0 then 3,4 at "Specify second point:" setting 5 as AutoCAD
        /// 2021 does; text that is no number refused with AutoCAD's "Requires numeric distance or two points." and asked
        /// again; set at once, no step of undo.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="get">Reads the value.</param>
        /// <param name="set">Sets a value taken.</param>
        /// <returns>The variable.</returns>
        /// <exception cref="ArgumentNullException">Thrown when either delegate is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the name is empty or has a space.</exception>
        public static CadSystemVariable Distance(string name, Func<double> get, Action<double> set)
        {
            Func<double> read = get ?? throw new ArgumentNullException(nameof(get));
            Action<double> write = set ?? throw new ArgumentNullException(nameof(set));
            return Number(name, false, RealBound.Finite, false, s => read(), (c, v) => write(v));
        }

        /// <summary>
        /// Makes a switch the app keeps, as LWDISPLAY: shown as OFF or ON, asked "Enter new value for LWDISPLAY &lt;OFF&gt;:",
        /// 0, 1, ON or OFF taken in either case, anything else refused with AutoCAD's "Requires 0 or 1 only, or option
        /// keyword." and asked again; set at once, no step of undo.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="get">Reads the value.</param>
        /// <param name="set">Sets a value taken.</param>
        /// <returns>The variable.</returns>
        /// <exception cref="ArgumentNullException">Thrown when either delegate is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the name is empty or has a space.</exception>
        public static CadSystemVariable Switch(string name, Func<bool> get, Action<bool> set)
        {
            Func<bool> read = get ?? throw new ArgumentNullException(nameof(get));
            Action<bool> write = set ?? throw new ArgumentNullException(nameof(set));
            return Switch(name, false, s => read(), (c, v) => write(v), null);
        }

        /// <inheritdoc/>
        public override string ToString() => Name;

        /// <summary>A switch shown as OFF or ON; one kept in the drawing set as a step of undo named by the record.</summary>
        internal static CadSystemVariable Switch(
            string name,
            bool inDrawing,
            Func<CadCommandSession, bool> get,
            Action<CadCommandContext, bool> set,
            string record
        )
        {
            var variable = new CadSystemVariable(name, CadVariableKind.Integer, inDrawing, s => get(s) ? "ON" : "OFF");
            variable._ask = c => AskSwitch(c, variable, get, set, record);
            variable._numbers = s => new[] { get(s) ? 1.0 : 0.0 };
            return variable;
        }

        private static GeoPoint3 P3(GeoPoint2 point) => new GeoPoint3(point.X, point.Y, 0.0);

        /// <summary>What a real number set has to be.</summary>
        internal enum RealBound
        {
            Finite,
            NonNegative,
            Positive,
        }

        /// <summary>A whole number; one kept in the drawing set as a step of undo named by the record.</summary>
        internal static CadSystemVariable Whole(
            string name,
            bool inDrawing,
            int least,
            int most,
            Func<CadCommandSession, int> get,
            Action<CadCommandContext, int> set,
            string record = null,
            Func<CadCommandContext, int, string> check = null
        )
        {
            if (least > most)
            {
                throw new ArgumentException("The least value of " + name + " is above its greatest.", nameof(least));
            }

            var variable = new CadSystemVariable(name, CadVariableKind.Integer, inDrawing, s =>
                get(s).ToString(CultureInfo.InvariantCulture)
            );
            variable._ask = c => AskWhole(c, variable, least, most, get, set, record, check);
            variable._numbers = s => new double[] { get(s) };
            return variable;
        }

        /// <summary>
        /// A real number, shown to four places or, for an angle, to as few as it needs; a value the check refuses is written
        /// off with the reason it gives and not asked again, as AutoCAD's "Cannot set MLEADERSCALE to that value.".
        /// </summary>
        internal static CadSystemVariable Number(
            string name,
            bool inDrawing,
            RealBound bound,
            bool plain,
            Func<CadCommandSession, double> get,
            Action<CadCommandContext, double> set,
            string record = null,
            double most = double.PositiveInfinity,
            Func<double, string> check = null
        )
        {
            var variable = new CadSystemVariable(name, CadVariableKind.Real, inDrawing, s => Show(get(s), plain));
            variable._ask = c => AskReal(c, variable, bound, plain, most, get, set, record, check);
            variable._numbers = s => new[] { get(s) };
            return variable;
        }

        /// <summary>A text: the take sets a text typed and gives null, or gives why it refused it.</summary>
        internal static CadSystemVariable Text(
            string name,
            bool inDrawing,
            Func<CadCommandSession, string> get,
            Func<CadCommandContext, string, string> take
        )
        {
            var variable = new CadSystemVariable(name, CadVariableKind.Text, inDrawing, s => "\"" + get(s) + "\"");
            variable._ask = c => AskText(c, variable, take);
            return variable;
        }

        /// <summary>A point kept in the drawing, shown with its Z where it has one.</summary>
        internal static CadSystemVariable Point(
            string name,
            bool withZ,
            Func<CadCommandSession, GeoPoint3> get,
            Action<CadCommandContext, GeoPoint2> set,
            string record
        ) => Point(name, true, withZ, get, set, record);

        /// <summary>A point, kept in the drawing or the app's, shown with its Z where it has one.</summary>
        private static CadSystemVariable Point(
            string name,
            bool inDrawing,
            bool withZ,
            Func<CadCommandSession, GeoPoint3> get,
            Action<CadCommandContext, GeoPoint2> set,
            string record
        )
        {
            var variable = new CadSystemVariable(name, CadVariableKind.Point, inDrawing, s => Show(get(s), withZ));
            variable._ask = c => AskPoint(c, variable, withZ, get, set, record);
            variable._numbers = s =>
            {
                GeoPoint3 point = get(s);
                return new[] { point.X, point.Y, withZ ? point.Z : 0.0 };
            };
            return variable;
        }

        /// <summary>A variable that can only be read; its numbers are those CAL's getvar gives, where it has any.</summary>
        internal static CadSystemVariable ReadOnly(
            string name,
            CadVariableKind kind,
            Func<CadCommandSession, string> show,
            Func<CadCommandSession, double[]> numbers = null
        ) => new CadSystemVariable(name, kind, false, show) { _numbers = numbers };

        /// <summary>
        /// The number or the point the variable holds, as CAL's <c>getvar</c> takes it: one value for a whole or real number
        /// (an angle in radians, a switch 0 or 1), three for a point (Z nought where the variable has two coordinates); null
        /// for a text, which <c>getvar</c> refuses.
        /// </summary>
        internal double[] GetNumbers(CadCommandSession session) => _numbers?.Invoke(session);

        /// <summary>
        /// Gives a variable of the drawing the kind its change is recorded as: a setting changed until set; a table changed for
        /// IMAGEFRAME, kept in the drawing's objects, so that every space is drawn again as it was before it was a variable.
        /// </summary>
        internal CadSystemVariable Recorded(CadEditRecordKind kind)
        {
            _recordKind = kind;
            return this;
        }

        /// <summary>Gives the variable its own prompts for when its name is typed alone.</summary>
        internal CadSystemVariable Asking(Func<CadCommandContext, IEnumerable<CadPrompt>> own)
        {
            Own = own;
            return this;
        }

        /// <summary>Asks the new value as SETVAR does, "Enter new value for OSMODE &lt;4133&gt;:", and sets it; one read only says its value.</summary>
        internal IEnumerable<CadPrompt> Ask(CadCommandContext c)
        {
            if (_ask == null)
            {
                c.Write(Name + " = " + GetValue(c.Session) + " (read only)");
                return Array.Empty<CadPrompt>();
            }

            return _ask(c);
        }

        internal static string Show(double value, bool plain) =>
            plain ? value.ToString("0.####", CultureInfo.InvariantCulture) : CadCommandContext.Length(value);

        internal static string Show(GeoPoint3 point, bool withZ) =>
            CadCommandContext.Length(point.X)
            + ","
            + CadCommandContext.Length(point.Y)
            + (withZ ? "," + CadCommandContext.Length(point.Z) : string.Empty);

        private string Message => "Enter new value for " + Name;

        private static IEnumerable<CadPrompt> AskWhole(
            CadCommandContext c,
            CadSystemVariable variable,
            int least,
            int most,
            Func<CadCommandSession, int> get,
            Action<CadCommandContext, int> set,
            string record,
            Func<CadCommandContext, int, string> check
        )
        {
            // A switch of 0 and 1 is refused as AutoCAD refuses ORTHOMODE 2; text, or a number not whole, as AutoCAD refuses
            // GRIDSTYLE abc and GRIDDISPLAY 2.5, in the same words.
            string range =
                least == 0 && most == 1
                    ? "Requires 0 or 1 only."
                    : "Requires an integer between "
                        + least.ToString(CultureInfo.InvariantCulture)
                        + " and "
                        + most.ToString(CultureInfo.InvariantCulture)
                        + ".";

            while (true)
            {
                int old = get(c.Session);
                yield return CadPrompt
                    .ForInteger(variable.Message, null, old.ToString(CultureInfo.InvariantCulture))
                    .Refusing(range);

                if (c.Input.Kind != CadInputKind.Number)
                {
                    yield break;
                }

                if (c.Input.Number < least || c.Input.Number > most)
                {
                    c.Write(range);
                    continue;
                }

                int value = (int)c.Input.Number;
                string refused = check?.Invoke(c, value);

                if (refused != null)
                {
                    c.Write(refused);
                    yield break;
                }

                if (value != old)
                {
                    variable.Store(
                        c,
                        record,
                        old.ToString(CultureInfo.InvariantCulture),
                        value.ToString(CultureInfo.InvariantCulture),
                        () => set(c, value)
                    );
                }

                yield break;
            }
        }

        private static IEnumerable<CadPrompt> AskReal(
            CadCommandContext c,
            CadSystemVariable variable,
            RealBound bound,
            bool plain,
            double most,
            Func<CadCommandSession, double> get,
            Action<CadCommandContext, double> set,
            string record,
            Func<double, string> check
        )
        {
            while (true)
            {
                double old = get(c.Session);
                yield return CadPrompt.ForDistance(variable.Message, null, null, Show(old, plain));

                if (
                    c.Input.Kind != CadInputKind.Number
                    || double.IsNaN(c.Input.Number)
                    || double.IsInfinity(c.Input.Number)
                )
                {
                    yield break;
                }

                double value = c.Input.Number;
                string refused = check?.Invoke(value);

                if (refused != null)
                {
                    c.Write(refused);
                    yield break;
                }

                if (bound == RealBound.Positive && !(value > 0.0))
                {
                    c.Write("Value must be positive and nonzero.");
                    continue;
                }

                if (bound == RealBound.NonNegative && value < 0.0)
                {
                    c.Write("Value must be positive or zero.");
                    continue;
                }

                if (value > most)
                {
                    c.Write("Value must be at most " + Show(most, true) + ".");
                    continue;
                }

                if (value != old)
                {
                    variable.Store(c, record, CadCommands.Values(old), CadCommands.Values(value), () => set(c, value));
                }

                yield break;
            }
        }

        /// <summary>An angle in the angle units of the session's drawing, as AutoCAD shows SNAPANG: 30, or 0.52r at AUNITS 3.</summary>
        private static string ShowAngle(CadCommandSession session, double radians)
        {
            DxfDatabase database = session.Document.Database;
            return CadUnits.Angle(radians, database.Aunits, database.Auprec);
        }

        private static IEnumerable<CadPrompt> AskAngle(
            CadCommandContext c,
            CadSystemVariable variable,
            Func<double> get,
            Action<double> set
        )
        {
            double old = get();
            yield return CadPrompt.ForAngle(variable.Message, null, null, ShowAngle(c.Session, old));

            if (
                c.Input.Kind != CadInputKind.Number
                || double.IsNaN(c.Input.Number)
                || double.IsInfinity(c.Input.Number)
            )
            {
                yield break;
            }

            double turn = 2.0 * Math.PI;
            double value = c.Input.Number % turn;
            value = value < 0.0 ? value + turn : value;
            value = value >= turn ? 0.0 : value;

            if (value != old)
            {
                set(value);
            }
        }

        private static IEnumerable<CadPrompt> AskText(
            CadCommandContext c,
            CadSystemVariable variable,
            Func<CadCommandContext, string, string> take
        )
        {
            yield return CadPrompt.ForString(variable.Message, variable.GetValue(c.Session));

            if (c.Input.Kind != CadInputKind.Text)
            {
                yield break;
            }

            string refused = take(c, c.Input.Text.Trim().Trim('"'));

            // An edit refused has said why already; the reason given is written where there is one.
            if (!string.IsNullOrEmpty(refused))
            {
                c.Write(refused);
            }
        }

        private static IEnumerable<CadPrompt> AskOptionalText(
            CadCommandContext c,
            CadSystemVariable variable,
            Func<string, bool> take
        )
        {
            yield return CadPrompt.ForString(variable.Message + ", or . for none", variable.GetValue(c.Session));

            if (c.Input.Kind != CadInputKind.Text)
            {
                yield break;
            }

            string typed = c.Input.Text.Trim();

            if (!take(typed == "." ? string.Empty : typed))
            {
                c.Write("Cannot set " + variable.Name + " to that value.");
            }
        }

        private static IEnumerable<CadPrompt> AskSwitch(
            CadCommandContext c,
            CadSystemVariable variable,
            Func<CadCommandSession, bool> get,
            Action<CadCommandContext, bool> set,
            string record
        )
        {
            const string refused = "Requires 0 or 1 only, or option keyword.";

            while (true)
            {
                bool old = get(c.Session);
                yield return CadPrompt
                    .ForInteger(variable.Message, null, old ? "ON" : "OFF")
                    .Hiding("ON", "OFF")
                    .Refusing(refused);

                bool value;

                if (c.Input.Is("ON") || c.Input.Is("OFF"))
                {
                    value = c.Input.Is("ON");
                }
                else if (c.Input.Kind == CadInputKind.Number && (c.Input.Number == 0.0 || c.Input.Number == 1.0))
                {
                    value = c.Input.Number == 1.0;
                }
                else if (c.Input.Kind == CadInputKind.Number)
                {
                    c.Write(refused);
                    continue;
                }
                else
                {
                    yield break;
                }

                if (value != old)
                {
                    variable.Store(c, record, old ? "1" : "0", value ? "1" : "0", () => set(c, value));
                }

                yield break;
            }
        }

        private static IEnumerable<CadPrompt> AskPointTaken(
            CadCommandContext c,
            CadSystemVariable variable,
            Func<GeoPoint2> get,
            Func<GeoPoint2, bool> take
        )
        {
            GeoPoint2 last = c.Session.LastPoint;
            yield return CadPrompt
                .ForPoint(variable.Message, null, null, Show(P3(get()), false))
                .MeasuringFromLastPoint()
                .Refusing("Invalid 2D point.");

            // The point typed is the variable's value, not a point given: AutoCAD 2021 leaves LASTPOINT where it was.
            c.Session.LastPoint = last;

            if (c.Input.IsPoint && !take(c.Input.Point))
            {
                c.Write("Cannot set " + variable.Name + " to that value.");
            }
        }

        private static IEnumerable<CadPrompt> AskPoint(
            CadCommandContext c,
            CadSystemVariable variable,
            bool withZ,
            Func<CadCommandSession, GeoPoint3> get,
            Action<CadCommandContext, GeoPoint2> set,
            string record
        )
        {
            GeoPoint3 old = get(c.Session);
            GeoPoint2 last = c.Session.LastPoint;
            CadPrompt prompt = CadPrompt
                .ForPoint(variable.Message, null, null, Show(old, withZ))
                .MeasuringFromLastPoint();

            // A point of two coordinates is refused as AutoCAD refuses SNAPBASE abc.
            yield return withZ ? prompt : prompt.Refusing("Invalid 2D point.");

            // The point typed is the variable's value, not a point given: AutoCAD 2021 leaves LASTPOINT where it was.
            c.Session.LastPoint = last;

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            GeoPoint2 value = c.Input.Point;

            // A point within the tolerance of the one kept is the same point, and changes nothing.
            if ((value - new GeoPoint2(old.X, old.Y)).Length > c.Session.Tolerance.EqualPoint)
            {
                variable.Store(c, record, CadCommands.Values(old.X, old.Y), CadCommands.Values(value.X, value.Y), () =>
                    set(c, value)
                );
            }
        }

        /// <summary>Sets a value: in the drawing as one step of undo, recorded as a setting changed; of the app at once.</summary>
        private void Store(CadCommandContext c, string record, string old, string value, Action set)
        {
            if (!IsInDrawing)
            {
                set();
                return;
            }

            c.Apply(new[] { new CadEditRecord(_recordKind, null, record ?? "$" + Name, old, value) }, s => set());
        }
    }
}
