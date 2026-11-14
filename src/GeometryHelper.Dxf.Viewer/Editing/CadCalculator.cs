using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// A value of the geometry calculator: a whole number, a real number or a vector (a point) of three coordinates, as
    /// AutoCAD's CAL keeps them.
    /// </summary>
    public readonly struct CadCalcValue
    {
        private CadCalcValue(bool isVector, bool isInteger, double x, double y, double z)
        {
            IsVector = isVector;
            IsInteger = isInteger;
            X = x;
            Y = y;
            Z = z;
        }

        /// <summary>Gets whether the value is a vector.</summary>
        public bool IsVector { get; }

        /// <summary>Gets whether the value is a whole number, as 1+2*3 is.</summary>
        public bool IsInteger { get; }

        /// <summary>Gets the number, or the X of a vector.</summary>
        public double X { get; }

        /// <summary>Gets the Y of a vector; nought for a number.</summary>
        public double Y { get; }

        /// <summary>Gets the Z of a vector; nought for a number.</summary>
        public double Z { get; }

        /// <summary>Gets the number; the X of a vector.</summary>
        public double Number => X;

        /// <summary>Gets the vector as a point; a number gives (number, 0, 0).</summary>
        public GeoPoint3 Point => new GeoPoint3(X, Y, Z);

        /// <summary>Makes a real number.</summary>
        /// <param name="value">The number.</param>
        /// <returns>The value.</returns>
        public static CadCalcValue Real(double value) => new CadCalcValue(false, false, value, 0.0, 0.0);

        /// <summary>Makes a whole number.</summary>
        /// <param name="value">The number.</param>
        /// <returns>The value.</returns>
        public static CadCalcValue Integer(long value) => new CadCalcValue(false, true, value, 0.0, 0.0);

        /// <summary>Makes a vector.</summary>
        /// <param name="x">The X.</param>
        /// <param name="y">The Y.</param>
        /// <param name="z">The Z.</param>
        /// <returns>The value.</returns>
        public static CadCalcValue Vector(double x, double y, double z) => new CadCalcValue(true, false, x, y, z);

        /// <summary>
        /// Writes the value as AutoCAD 2021's CAL writes it on the command line, whatever the drawing's units: a whole number
        /// as 7, a real number to nine significant digits (6, 0.333333333, 53.1301024, 1e+10), a vector as 120,80,0.
        /// </summary>
        /// <returns>The text.</returns>
        public override string ToString()
        {
            if (IsVector)
            {
                return Write(X) + "," + Write(Y) + "," + Write(Z);
            }

            return IsInteger ? ((long)X).ToString(CultureInfo.InvariantCulture) : Write(X);
        }

        /// <summary>Writes the value as QuickCalc puts it into its input, which CAL reads back: 7, 1.41421, [1,2,0].</summary>
        /// <returns>The text.</returns>
        public string ToInput()
        {
            if (IsVector)
            {
                return "[" + Plain(X) + "," + Plain(Y) + "," + Plain(Z) + "]";
            }

            return IsInteger ? ((long)X).ToString(CultureInfo.InvariantCulture) : Plain(X);
        }

        private static string Plain(double value) =>
            (Math.Abs(value) < 1E-12 ? 0.0 : value).ToString("0.##########", CultureInfo.InvariantCulture);

        /// <summary>
        /// Writes the value as the text AutoCAD types into the prompt a transparent CAL answers: the number, or x,y,z, to every
        /// digit it holds.
        /// </summary>
        /// <returns>The text.</returns>
        public string ToTyped()
        {
            if (IsVector)
            {
                return Typed(X) + "," + Typed(Y) + "," + Typed(Z);
            }

            return IsInteger ? ((long)X).ToString(CultureInfo.InvariantCulture) : Typed(X);
        }

        private static string Typed(double value) =>
            (Math.Abs(value) < 1E-12 ? 0.0 : value).ToString("R", CultureInfo.InvariantCulture);

        // The %.9g of C: nine significant digits, the plain form or the one with an exponent, whichever C picks, no trailing zeros.
        private static string Write(double value) =>
            (Math.Abs(value) < 1E-12 ? 0.0 : value).ToString("G9", CultureInfo.InvariantCulture).Replace('E', 'e');
    }

    /// <summary>A mistake in an expression of the geometry calculator, its message in AutoCAD 2021's words.</summary>
    public sealed class CadCalcException : Exception
    {
        /// <summary>Makes the mistake.</summary>
        /// <param name="message">What AutoCAD says: "Division by 0", "Function FOO doesn't exist".</param>
        public CadCalcException(string message)
            : base(message) { }
    }

    /// <summary>What an expression asks for in the drawing, in the order it asks: a point, an object to snap to, a circle or an arc.</summary>
    public enum CadCalcInputKind
    {
        /// <summary>A point, <c>cur</c>: "&gt;&gt; Enter a point:".</summary>
        Point,

        /// <summary>An object, and the object snap of it that gives a point: "&gt;&gt; Select entity for END snap:".</summary>
        Snap,

        /// <summary>A circle, an arc or a polyline segment, whose radius <c>rad</c> gives: "&gt;&gt; Select circle, arc or polyline segment for RAD function:".</summary>
        Radius,
    }

    /// <summary>One thing an expression of the geometry calculator asks for in the drawing before it can be worked out.</summary>
    public sealed class CadCalcInput
    {
        internal CadCalcInput(CadCalcInputKind kind, string mode)
        {
            Kind = kind;
            Mode = mode;
        }

        /// <summary>Gets what is asked for.</summary>
        public CadCalcInputKind Kind { get; }

        /// <summary>Gets the object snap of a <see cref="CadCalcInputKind.Snap"/>: END, INS, INT, MID, CEN, NEA, NOD, QUA, PER or TAN; empty for the others.</summary>
        public string Mode { get; }

        /// <summary>Gets the prompt AutoCAD asks it with, without the colon.</summary>
        public string Prompt =>
            Kind == CadCalcInputKind.Point ? ">> Enter a point"
            : Kind == CadCalcInputKind.Snap ? ">> Select entity for " + Mode + " snap"
            : ">> Select circle, arc or polyline segment for RAD function";
    }

    /// <summary>
    /// The geometry calculator of CAL and QuickCalc, as AutoCAD 2021's: numbers, vectors written <c>[x,y,z]</c> (a missing
    /// coordinate nought), <c>+ - * / ^</c> and brackets, the functions <c>sin cos tang asin acos atan</c> (in degrees),
    /// <c>ln log exp exp10 sqr sqrt abs round trunc r2d d2r</c> (<c>abs</c> of a vector is its length), the constant
    /// <c>pi</c>, of points <c>dist(p1,p2)</c>, <c>ang(p1,p2)</c>, <c>ang(v)</c>, <c>ang(apex,p1,p2)</c>, <c>ill(p1,p2,p3,p4)</c>,
    /// <c>ilp</c>, <c>vec</c>, <c>vec1</c>, <c>nor</c>, <c>dpl</c>, <c>dpp</c>, <c>plt</c>, <c>pld</c>, the conversion
    /// <c>cvunit(value,from,to)</c>, and what is picked in the drawing: <c>cur</c> a point, <c>rad</c> the radius of a circle or an
    /// arc, the snaps <c>end ins int mid cen nea nod qua per tan</c> the point of an object picked, and <c>mee</c>, <c>vee</c>,
    /// <c>vee1</c>, <c>dee</c> and <c>ille</c>, which pick the ends of lines (<see cref="Inputs"/>), and in a drawing (the overload with a
    /// session) <c>getvar(name)</c>, the number or point of a system variable, <c>w2u</c> and <c>u2w</c>, between the world and the
    /// user coordinate system, <c>rot</c>, and <c>xof</c>, <c>yof</c>, <c>zof</c>, <c>xyof</c>, <c>rxof</c>, <c>ryof</c> and <c>rzof</c>.
    /// </summary>
    /// <remarks>
    /// <c>1+2*3</c> is 7, <c>sqrt(16)+2</c> 6, <c>dist([0,0],[300,400])</c> 500, <c>ang([0,0],[300,400])</c> 53.1301024 and
    /// <c>[100,50]+[20,30]</c> 120,80,0, as CAL writes them (<see cref="CadCalcValue.ToString"/>). A number added to a vector is
    /// refused as AutoCAD refuses it: "Operators '+', '-' require operands of the same type".
    /// </remarks>
    public static class CadCalculator
    {
        private static readonly HashSet<string> RealFunctions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "SIN",
            "COS",
            "TANG",
            "ASIN",
            "ACOS",
            "ATAN",
            "LN",
            "LOG",
            "EXP",
            "EXP10",
            "SQR",
            "SQRT",
            "ABS",
            "ROUND",
            "TRUNC",
            "R2D",
            "D2R",
        };

        // The object snaps a name picks an object for: end snaps its ends, tan the point a line from the last point touches.
        private static readonly HashSet<string> SnapNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "END",
            "INS",
            "INT",
            "MID",
            "CEN",
            "NEA",
            "NOD",
            "QUA",
            "PER",
            "TAN",
        };

        // The functions of points, which take their arguments in brackets.
        private static readonly HashSet<string> PointFunctions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "DIST",
            "ANG",
            "ILL",
            "ILP",
            "VEC",
            "VEC1",
            "NOR",
            "DPL",
            "DPP",
            "PLT",
            "PLD",
            "CVUNIT",
        };

        // The names of the functions and constants that take the place of a variable's name, which getvar refuses: getvar(pi) is
        // asked for a name, as getvar(1) is.
        private static readonly HashSet<string> KnownNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "PI",
            "CUR",
            "RAD",
            "MEE",
            "VEE",
            "VEE1",
            "DEE",
            "ILLE",
            "GETVAR",
            "ROT",
        };

        // w2u, u2w and the functions that take a vector apart, each as AutoCAD spells it in its messages.
        private static readonly Dictionary<string, string> VectorFunctions = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase
        )
        {
            { "W2U", "W2U" },
            { "U2W", "U2W" },
            { "XOF", "Xof" },
            { "YOF", "Yof" },
            { "ZOF", "Zof" },
            { "XYOF", "XYof" },
            { "RXOF", "rXof" },
            { "RYOF", "rYof" },
            { "RZOF", "rZof" },
        };

        // The units cvunit knows: length in metres, angles in degrees. A small part of AutoCAD's acad.unt.
        private static readonly Dictionary<string, KeyValuePair<char, double>> Units = MakeUnits();

        /// <summary>Counts the points an expression picks in the drawing: each <c>cur</c> in it, as CAL asks one "&gt;&gt; Enter a point:" for each.</summary>
        /// <param name="expression">The expression.</param>
        /// <returns>How many.</returns>
        public static int PointsPicked(string expression)
        {
            int count = 0;

            foreach (CadCalcInput input in Inputs(expression))
            {
                if (input.Kind == CadCalcInputKind.Point)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Lists what an expression asks for in the drawing, in the order CAL asks it: <c>dist(end,end)</c> two object snaps,
        /// <c>cur</c> a point, <c>rad</c> a radius, <c>vee</c> two END snaps. An expression that is wrong from some place on lists
        /// what comes before it.
        /// </summary>
        /// <param name="expression">The expression.</param>
        /// <returns>The inputs.</returns>
        public static IReadOnlyList<CadCalcInput> Inputs(string expression)
        {
            var record = new List<CadCalcInput>();

            try
            {
                var parser = new Parser(expression ?? string.Empty, null, record, null);
                parser.Expression();
            }
            catch (CadCalcException)
            {
                // What was found before the mistake is what CAL asks first.
            }

            return record;
        }

        /// <summary>Evaluates an expression whose points <c>cur</c> are given.</summary>
        /// <param name="expression">The expression.</param>
        /// <param name="picked">The points <c>cur</c> takes, in the order the expression names them; null where it names none.</param>
        /// <returns>The value.</returns>
        /// <exception cref="CadCalcException">Thrown when the expression is wrong, in AutoCAD's words.</exception>
        public static CadCalcValue Evaluate(string expression, IReadOnlyList<GeoPoint3> picked = null)
        {
            var answers = new List<CadCalcValue>();

            if (picked != null)
            {
                foreach (GeoPoint3 point in picked)
                {
                    answers.Add(CadCalcValue.Vector(point.X, point.Y, point.Z));
                }
            }

            return Evaluate(expression, answers);
        }

        /// <summary>Evaluates an expression whose inputs are given.</summary>
        /// <param name="expression">The expression.</param>
        /// <param name="answers">What <see cref="Inputs"/> lists, in that order: a vector for a point or an object snap, a number for a radius.</param>
        /// <returns>The value.</returns>
        /// <exception cref="CadCalcException">Thrown when the expression is wrong, in AutoCAD's words.</exception>
        public static CadCalcValue Evaluate(string expression, IReadOnlyList<CadCalcValue> answers) =>
            Evaluate(expression, answers, null);

        /// <summary>
        /// Evaluates an expression whose inputs are given, in a drawing: <c>getvar</c> reads the system variables the session
        /// knows, and <c>w2u</c> and <c>u2w</c> change points between the world coordinate system and the session's current
        /// user coordinate system.
        /// </summary>
        /// <param name="expression">The expression.</param>
        /// <param name="answers">What <see cref="Inputs"/> lists, in that order: a vector for a point or an object snap, a number for a radius.</param>
        /// <param name="session">The command line of the drawing; null for none, as the other overload: no variable is known and the user system is the world one.</param>
        /// <returns>The value.</returns>
        /// <exception cref="CadCalcException">Thrown when the expression is wrong, in AutoCAD's words.</exception>
        public static CadCalcValue Evaluate(
            string expression,
            IReadOnlyList<CadCalcValue> answers,
            CadCommandSession session
        )
        {
            var parser = new Parser(expression ?? string.Empty, answers, null, session);
            CadCalcValue value = parser.Expression();
            parser.SkipSpaces();

            if (!parser.AtEnd)
            {
                char c = parser.Current;
                throw new CadCalcException(
                    c == ')' || char.IsLetterOrDigit(c) ? "Wrong syntax of expression" : "Illegal character '" + c + "'"
                );
            }

            return value;
        }

        private static Dictionary<string, KeyValuePair<char, double>> MakeUnits()
        {
            var units = new Dictionary<string, KeyValuePair<char, double>>(StringComparer.OrdinalIgnoreCase);

            void Length(double metres, params string[] names)
            {
                foreach (string name in names)
                {
                    units[name] = new KeyValuePair<char, double>('L', metres);
                }
            }

            void Angle(double degrees, params string[] names)
            {
                foreach (string name in names)
                {
                    units[name] = new KeyValuePair<char, double>('A', degrees);
                }
            }

            Length(1E-6, "micron", "microns");
            Length(1E-3, "mm", "millimeter", "millimeters", "millimetre", "millimetres");
            Length(1E-2, "cm", "centimeter", "centimeters", "centimetre", "centimetres");
            Length(0.1, "dm", "decimeter", "decimeters");
            Length(1.0, "m", "meter", "meters", "metre", "metres");
            Length(1000.0, "km", "kilometer", "kilometers", "kilometre", "kilometres");
            Length(0.0000254, "mil", "mils", "thou");
            Length(0.0254, "in", "inch", "inches", "\"");
            Length(0.3048, "ft", "foot", "feet", "'");
            Length(0.9144, "yd", "yard", "yards");
            Length(1609.344, "mi", "mile", "miles");
            Angle(1.0, "deg", "degree", "degrees");
            Angle(180.0 / Math.PI, "rad", "radian", "radians");
            Angle(0.9, "grad", "grads", "gradian", "gradians");
            return units;
        }

        private sealed class Parser
        {
            private const string Short = "Use ";
            private const string GetVarForm = "GETVAR(AutoCAD_system_variable)";
            private const string RotForm = "ROT(p,origin,angle) or ROT(p,p1ax,p2ax,angle)";

            private readonly string _text;
            private readonly IReadOnlyList<CadCalcValue> _answers;
            private readonly List<CadCalcInput> _record;
            private readonly CadCommandSession _session;
            private int _at;
            private int _next;

            public Parser(
                string text,
                IReadOnlyList<CadCalcValue> answers,
                List<CadCalcInput> record,
                CadCommandSession session
            )
            {
                _text = text;
                _session = session;
                _answers = answers;
                _record = record;
            }

            public bool AtEnd => _at >= _text.Length;

            public char Current => _text[_at];

            // Listing the inputs works the expression through with a stand-in for each, no sum done.
            private bool IsListing => _record != null;

            public void SkipSpaces()
            {
                while (!AtEnd && char.IsWhiteSpace(Current))
                {
                    _at++;
                }
            }

            private bool Take(char c)
            {
                SkipSpaces();

                if (!AtEnd && Current == c)
                {
                    _at++;
                    return true;
                }

                return false;
            }

            public CadCalcValue Expression()
            {
                CadCalcValue value = Term();

                while (true)
                {
                    if (Take('+'))
                    {
                        CadCalcValue right = Term();
                        value = IsListing ? value : Add(value, right, 1.0);
                    }
                    else if (Take('-'))
                    {
                        CadCalcValue right = Term();
                        value = IsListing ? value : Add(value, right, -1.0);
                    }
                    else
                    {
                        return value;
                    }
                }
            }

            private CadCalcValue Term()
            {
                CadCalcValue value = Power();

                while (true)
                {
                    if (Take('*'))
                    {
                        CadCalcValue right = Power();
                        value = IsListing ? value : Multiply(value, right);
                    }
                    else if (Take('/'))
                    {
                        CadCalcValue right = Power();
                        value = IsListing ? value : Divide(value, right);
                    }
                    else
                    {
                        return value;
                    }
                }
            }

            // ^ takes its operands from the left, as AutoCAD's does: 2^3^2 is 64.
            private CadCalcValue Power()
            {
                CadCalcValue value = Unary();

                while (Take('^'))
                {
                    CadCalcValue exponent = Unary();

                    if (IsListing)
                    {
                        continue;
                    }

                    if (value.IsVector || exponent.IsVector)
                    {
                        throw new CadCalcException("Power operator '^' requires real operands");
                    }

                    if (value.Number < 0.0 && !exponent.IsInteger)
                    {
                        throw new CadCalcException("Negative numbers cannot be raised to real exponent");
                    }

                    value = Checked(Math.Pow(value.Number, exponent.Number));
                }

                return value;
            }

            private CadCalcValue Unary()
            {
                if (Take('-'))
                {
                    CadCalcValue value = Unary();
                    return value.IsVector ? CadCalcValue.Vector(-value.X, -value.Y, -value.Z)
                        : value.IsInteger ? CadCalcValue.Integer(-(long)value.X)
                        : CadCalcValue.Real(-value.X);
                }

                if (Take('+'))
                {
                    return Unary();
                }

                return Primary();
            }

            private CadCalcValue Primary()
            {
                SkipSpaces();

                if (AtEnd)
                {
                    throw new CadCalcException("Unexpected end of expression");
                }

                char c = Current;

                if (c == '(')
                {
                    _at++;
                    CadCalcValue value = Expression();

                    if (!Take(')'))
                    {
                        throw new CadCalcException("Right parenthesis ')' missing");
                    }

                    return value;
                }

                if (c == '[')
                {
                    _at++;
                    return VectorOf();
                }

                if (char.IsDigit(c) || c == '.')
                {
                    return NumberOf();
                }

                if (char.IsLetter(c))
                {
                    return Named();
                }

                throw new CadCalcException("Illegal character '" + c + "'");
            }

            private CadCalcValue VectorOf()
            {
                var parts = new double[3];

                for (int i = 0; i < 3; i++)
                {
                    SkipSpaces();
                    bool empty = !AtEnd && (Current == ',' || Current == ']');

                    if (!empty)
                    {
                        CadCalcValue part = Expression();

                        if (part.IsVector)
                        {
                            throw new CadCalcException("Vector components must be real numbers");
                        }

                        parts[i] = part.Number;
                    }

                    if (Take(']'))
                    {
                        return CadCalcValue.Vector(parts[0], parts[1], parts[2]);
                    }

                    if (i < 2 && !Take(','))
                    {
                        SkipSpaces();
                        throw new CadCalcException(
                            AtEnd
                                ? "Wrong syntax of vector, maybe ']' missing"
                                : "Wrong syntax of vector, maybe ',' missing"
                        );
                    }
                }

                throw new CadCalcException("Wrong syntax of vector, maybe ']' missing");
            }

            private CadCalcValue NumberOf()
            {
                int start = _at;
                bool real = false;

                while (!AtEnd && (char.IsDigit(Current) || Current == '.'))
                {
                    real |= Current == '.';
                    _at++;
                }

                if (!AtEnd && (Current == 'e' || Current == 'E'))
                {
                    int mark = _at;
                    _at++;

                    if (!AtEnd && (Current == '+' || Current == '-'))
                    {
                        _at++;
                    }

                    if (AtEnd || !char.IsDigit(Current))
                    {
                        _at = mark;
                    }
                    else
                    {
                        real = true;

                        while (!AtEnd && char.IsDigit(Current))
                        {
                            _at++;
                        }
                    }
                }

                string text = _text.Substring(start, _at - start);

                if (
                    !real
                    && long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out long whole)
                    && whole <= int.MaxValue
                )
                {
                    return CadCalcValue.Integer(whole);
                }

                if (!real)
                {
                    throw new CadCalcException("Integer numbers must be between 2147483647 and -2147483648!");
                }

                if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                {
                    throw new CadCalcException("Wrong real number '" + text + "'");
                }

                return CadCalcValue.Real(value);
            }

            private CadCalcValue Named()
            {
                int start = _at;

                while (!AtEnd && (char.IsLetterOrDigit(Current) || Current == '_'))
                {
                    _at++;
                }

                string name = _text.Substring(start, _at - start);
                string upper = name.ToUpperInvariant();

                if (!Take('('))
                {
                    return Bare(name, upper);
                }

                if (upper == "GETVAR")
                {
                    return GetVar();
                }

                if (SnapNames.Contains(upper))
                {
                    throw new CadCalcException("Function _" + upper + " takes no arguments");
                }

                if (upper == "CUR" || upper == "RAD" || IsPairFunction(upper))
                {
                    throw new CadCalcException("Function " + upper + " takes no arguments");
                }

                if (upper == "CVUNIT")
                {
                    return ConvertUnits();
                }

                var args = new List<CadCalcValue>();

                if (!Take(')'))
                {
                    do
                    {
                        args.Add(Expression());
                    } while (Take(','));

                    if (!Take(')'))
                    {
                        throw new CadCalcException("Right parenthesis ')' missing");
                    }
                }

                if (IsListing)
                {
                    return CadCalcValue.Real(1.0);
                }

                return upper == "ROT" || VectorFunctions.ContainsKey(upper)
                    ? Transform(upper, args)
                    : Call(upper, args);
            }

            private static bool IsPairFunction(string name) =>
                name == "MEE" || name == "VEE" || name == "VEE1" || name == "DEE" || name == "ILLE";

            // A name with no brackets: a constant, or what is picked in the drawing.
            private CadCalcValue Bare(string name, string upper)
            {
                switch (upper)
                {
                    case "PI":
                        return CadCalcValue.Real(Math.PI);
                    case "CUR":
                        return Pick(new CadCalcInput(CadCalcInputKind.Point, string.Empty));
                    case "RAD":
                        return Pick(new CadCalcInput(CadCalcInputKind.Radius, string.Empty));
                    case "MEE":
                    case "VEE":
                    case "VEE1":
                    case "DEE":
                        CadCalcValue a = Pick(new CadCalcInput(CadCalcInputKind.Snap, "END"));
                        CadCalcValue b = Pick(new CadCalcInput(CadCalcInputKind.Snap, "END"));

                        if (IsListing)
                        {
                            return a;
                        }

                        return upper == "MEE"
                                ? CadCalcValue.Vector((a.X + b.X) / 2.0, (a.Y + b.Y) / 2.0, (a.Z + b.Z) / 2.0)
                            : upper == "DEE" ? Checked(a.Point.DistanceTo(b.Point))
                            : Unit(upper == "VEE1", CadCalcValue.Vector(b.X - a.X, b.Y - a.Y, b.Z - a.Z));
                    case "ILLE":
                        var four = new List<CadCalcValue>();

                        for (int i = 0; i < 4; i++)
                        {
                            four.Add(Pick(new CadCalcInput(CadCalcInputKind.Snap, "END")));
                        }

                        return IsListing ? four[0] : Intersection(four);
                    default:
                        if (SnapNames.Contains(upper))
                        {
                            return Pick(new CadCalcInput(CadCalcInputKind.Snap, upper));
                        }

                        if (upper == "GETVAR")
                        {
                            throw new CadCalcException(Short + GetVarForm);
                        }

                        if (upper == "ROT")
                        {
                            throw new CadCalcException(Short + RotForm);
                        }

                        if (VectorFunctions.TryGetValue(upper, out string spelled))
                        {
                            throw new CadCalcException("Function " + spelled + " requires one argument");
                        }

                        throw new CadCalcException(
                            RealFunctions.Contains(upper) || PointFunctions.Contains(upper)
                                ? "Function " + upper + " requires one argument"
                                : "AutoLISP variable " + name + " has no value"
                        );
                }
            }

            // getvar(name): the name is read as it stands, not worked out, up to the bracket that ends it; a number or a point of
            // the session's variables, as AutoCAD's CAL takes one.
            private CadCalcValue GetVar()
            {
                SkipSpaces();
                int start = _at;

                while (!AtEnd && Current != ')' && Current != ',' && Current != '(' && !char.IsWhiteSpace(Current))
                {
                    _at++;
                }

                string token = _text.Substring(start, _at - start);
                string upper = token.ToUpperInvariant();

                if (
                    token.Length == 0
                    || !(char.IsLetter(token[0]) || token[0] == '"' || token[0] == '_')
                    || RealFunctions.Contains(upper)
                    || PointFunctions.Contains(upper)
                    || SnapNames.Contains(upper)
                    || KnownNames.Contains(upper)
                    || VectorFunctions.ContainsKey(upper)
                )
                {
                    throw new CadCalcException(Short + GetVarForm);
                }

                if (!Take(')'))
                {
                    throw new CadCalcException("Right parenthesis ')' missing");
                }

                if (IsListing)
                {
                    return CadCalcValue.Real(1.0);
                }

                CadSystemVariable variable = _session?.Registry.FindVariable(token);

                if (variable == null)
                {
                    throw new CadCalcException("Unknown AutoCAD system variable " + upper);
                }

                double[] numbers = variable.GetNumbers(_session);

                if (numbers == null)
                {
                    throw new CadCalcException("Only real, integer or point AutoCAD system variables accepted");
                }

                return variable.Kind == CadVariableKind.Point ? CadCalcValue.Vector(numbers[0], numbers[1], numbers[2])
                    : variable.Kind == CadVariableKind.Integer ? CadCalcValue.Integer((long)numbers[0])
                    : Checked(numbers[0]);
            }

            // w2u, u2w, rot and the functions that take a vector apart.
            private CadCalcValue Transform(string name, List<CadCalcValue> args)
            {
                if (name == "ROT")
                {
                    return Rotate(args);
                }

                string spelled = VectorFunctions[name];

                if (args.Count != 1)
                {
                    throw new CadCalcException("Function " + spelled + " requires one argument");
                }

                CadCalcValue arg = args[0];
                bool alone = name == "XOF" || name == "YOF" || name == "ZOF";

                if (!arg.IsVector && !alone)
                {
                    throw new CadCalcException("Function " + spelled + " requires vector argument");
                }

                switch (name)
                {
                    case "W2U":
                    case "U2W":
                        if (_session == null || _session.Ucs.IsWorld)
                        {
                            return arg;
                        }

                        GeoPoint3 point =
                            name == "W2U" ? _session.Ucs.ToUcs(arg.Point) : _session.Ucs.ToWorld(arg.Point);
                        return CadCalcValue.Vector(point.X, point.Y, point.Z);
                    case "XOF":
                        return CadCalcValue.Vector(arg.X, 0.0, 0.0);
                    case "YOF":
                        return CadCalcValue.Vector(0.0, arg.IsVector ? arg.Y : arg.X, 0.0);
                    case "ZOF":
                        return CadCalcValue.Vector(0.0, 0.0, arg.IsVector ? arg.Z : arg.X);
                    case "XYOF":
                        return CadCalcValue.Vector(arg.X, arg.Y, 0.0);
                    case "RXOF":
                        return CadCalcValue.Real(arg.X);
                    case "RYOF":
                        return CadCalcValue.Real(arg.Y);
                    default:
                        return CadCalcValue.Real(arg.Z);
                }
            }

            // rot(p,origin,angle) turns a point about the line through the origin parallel to the Z axis; rot(p,p1ax,p2ax,angle)
            // about the line through two points; the angle in degrees, anticlockwise looking from the end of the axis.
            private static CadCalcValue Rotate(List<CadCalcValue> args)
            {
                bool three = args.Count == 3;
                Wrong(
                    (!three && args.Count != 4)
                        || args.GetRange(0, args.Count - 1).Exists(a => !a.IsVector)
                        || args[args.Count - 1].IsVector,
                    RotForm
                );
                CadCalcValue p = args[0];
                CadCalcValue origin = args[1];
                CadCalcValue axis = three ? CadCalcValue.Vector(0.0, 0.0, 1.0) : Between(origin, args[2]);
                double length = Length(axis);

                if (length < 1E-12)
                {
                    throw new CadCalcException("The points 'p1ax' and 'p2ax' in ROT(p,p1ax,p2ax,angle) coincide");
                }

                CadCalcValue k = CadCalcValue.Vector(axis.X / length, axis.Y / length, axis.Z / length);
                CadCalcValue v = Between(origin, p);

                if (Length(Cross(k, v)) < 1E-12 * Math.Max(1.0, Length(v)))
                {
                    throw new CadCalcException("The point to be rotated lies on the axis of rotation");
                }

                double angle = args[args.Count - 1].Number * Math.PI / 180.0;
                double cos = Math.Cos(angle);
                double sin = Math.Sin(angle);
                CadCalcValue turned = Cross(k, v);
                double along = Dot(k, v) * (1.0 - cos);
                return Checked3(
                    origin.X + v.X * cos + turned.X * sin + k.X * along,
                    origin.Y + v.Y * cos + turned.Y * sin + k.Y * along,
                    origin.Z + v.Z * cos + turned.Z * sin + k.Z * along
                );
            }

            private static CadCalcValue Checked3(double x, double y, double z)
            {
                Checked(x);
                Checked(y);
                Checked(z);
                return CadCalcValue.Vector(x, y, z);
            }

            // The answer to the next input; while listing, the input is noted and a stand-in given.
            private CadCalcValue Pick(CadCalcInput input)
            {
                if (IsListing)
                {
                    _record.Add(input);
                    return input.Kind == CadCalcInputKind.Radius
                        ? CadCalcValue.Real(1.0)
                        : CadCalcValue.Vector(1.0, 2.0, 3.0);
                }

                if (_answers == null || _next >= _answers.Count)
                {
                    throw new CadCalcException("No object picked.");
                }

                return _answers[_next++];
            }

            // cvunit(value,from,to): the units are words, not expressions.
            private CadCalcValue ConvertUnits()
            {
                CadCalcValue value = Expression();

                if (!Take(','))
                {
                    throw new CadCalcException(Short + "CVUNIT(value,from,to)");
                }

                string from = Word();

                if (!Take(','))
                {
                    throw new CadCalcException(Short + "CVUNIT(value,from,to)");
                }

                string to = Word();

                if (!Take(')'))
                {
                    throw new CadCalcException("Right parenthesis ')' missing");
                }

                if (IsListing)
                {
                    return value;
                }

                if (
                    value.IsVector
                    || !Units.TryGetValue(from, out KeyValuePair<char, double> a)
                    || !Units.TryGetValue(to, out KeyValuePair<char, double> b)
                    || a.Key != b.Key
                )
                {
                    throw new CadCalcException("CVUNIT failed to convert units");
                }

                return Checked(value.Number * a.Value / b.Value);
            }

            private string Word()
            {
                SkipSpaces();
                int start = _at;

                while (!AtEnd && Current != ',' && Current != ')')
                {
                    _at++;
                }

                return _text.Substring(start, _at - start).Trim().Trim('"');
            }

            private static CadCalcValue Call(string name, List<CadCalcValue> args)
            {
                if (RealFunctions.Contains(name))
                {
                    if (args.Count != 1)
                    {
                        throw new CadCalcException(
                            args.Count == 0
                                ? "Function " + name + " requires one argument"
                                : "Too many arguments to function " + name
                        );
                    }

                    if (args[0].IsVector)
                    {
                        if (name == "ABS")
                        {
                            return Checked(Length(args[0]));
                        }

                        throw new CadCalcException("Function " + name + " requires real argument");
                    }

                    return Real(name, args[0]);
                }

                switch (name)
                {
                    case "DIST":
                        Points(name, args, 2);
                        return Checked(args[0].Point.DistanceTo(args[1].Point));
                    case "ANG":
                        return Angle(args);
                    case "ILL":
                        Points(name, args, 4);
                        return Intersection(args);
                    case "ILP":
                        Wrong(args.Count != 5 || args.Exists(a => !a.IsVector), "ILP(p1,p2,p3,p4,p5)");
                        Points(name, args, 5);
                        return LinePlane(args);
                    case "VEC":
                    case "VEC1":
                        Wrong(args.Count != 2 || args.Exists(a => !a.IsVector), name + "(FromPt,ToPt)");
                        Points(name, args, 2);
                        return Unit(
                            name == "VEC1",
                            CadCalcValue.Vector(args[1].X - args[0].X, args[1].Y - args[0].Y, args[1].Z - args[0].Z)
                        );
                    case "NOR":
                        return Normal(args);
                    case "DPL":
                        Wrong(args.Count != 3 || args.Exists(a => !a.IsVector), "DPL(p,p1,p2)");
                        Points(name, args, 3);
                        return DistanceToLine(args);
                    case "DPP":
                        Wrong(args.Count != 4 || args.Exists(a => !a.IsVector), "DPP(p,p1,p2,p3)");
                        Points(name, args, 4);
                        return DistanceToPlane(args);
                    case "PLT":
                        Wrong(
                            args.Count != 3 || !args[0].IsVector || !args[1].IsVector || args[2].IsVector,
                            "PLT(p1,p2,t)"
                        );
                        return Along(args[0], args[1], args[2].Number * Length(Between(args[0], args[1])));
                    case "PLD":
                        Wrong(
                            args.Count != 3 || !args[0].IsVector || !args[1].IsVector || args[2].IsVector,
                            "PLD(p1,p2,dist)"
                        );
                        return Along(args[0], args[1], args[2].Number);
                    default:
                        throw new CadCalcException("Function " + name + " doesn't exist");
                }
            }

            private static void Wrong(bool wrong, string form)
            {
                if (wrong)
                {
                    throw new CadCalcException(Short + form);
                }
            }

            private static void Points(string name, List<CadCalcValue> args, int count)
            {
                if (args.Count < count)
                {
                    throw new CadCalcException("Unexpected argument list for function " + name);
                }

                if (args.Count > count)
                {
                    throw new CadCalcException("Too many arguments to function " + name);
                }

                if (args.Exists(a => !a.IsVector))
                {
                    throw new CadCalcException("Function " + name + " requires vector argument");
                }
            }

            private static CadCalcValue Between(CadCalcValue a, CadCalcValue b) =>
                CadCalcValue.Vector(b.X - a.X, b.Y - a.Y, b.Z - a.Z);

            private static double Length(CadCalcValue v) => Math.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);

            private static CadCalcValue Unit(bool unit, CadCalcValue v)
            {
                if (!unit)
                {
                    return v;
                }

                double length = Length(v);

                if (length < 1E-12)
                {
                    throw new CadCalcException("Division by 0");
                }

                return CadCalcValue.Vector(v.X / length, v.Y / length, v.Z / length);
            }

            // The point a distance from the first point along the way to the second.
            private static CadCalcValue Along(CadCalcValue a, CadCalcValue b, double distance)
            {
                CadCalcValue way = Unit(true, Between(a, b));
                return CadCalcValue.Vector(a.X + way.X * distance, a.Y + way.Y * distance, a.Z + way.Z * distance);
            }

            private static CadCalcValue Cross(CadCalcValue a, CadCalcValue b) =>
                CadCalcValue.Vector(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

            private static double Dot(CadCalcValue a, CadCalcValue b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

            // nor(v) and nor(p1,p2): the unit vector at a right angle to a 2D vector, to the left of it; nor(p1,p2,p3): the unit
            // normal of the plane through three points.
            private static CadCalcValue Normal(List<CadCalcValue> args)
            {
                if (args.Count == 1 || args.Count == 2)
                {
                    Points("NOR", args, args.Count);
                    CadCalcValue v = args.Count == 1 ? args[0] : Between(args[0], args[1]);
                    double length = Math.Sqrt(v.X * v.X + v.Y * v.Y);

                    if (length < 1E-12)
                    {
                        throw new CadCalcException("The 2D vector in NOR function is zero length");
                    }

                    return CadCalcValue.Vector(-v.Y / length, v.X / length, 0.0);
                }

                Wrong(args.Count != 3, "NOR(v), NOR(p1,p2) or NOR(p1,p2,p3)");
                Points("NOR", args, 3);
                CadCalcValue normal = Cross(Between(args[0], args[1]), Between(args[0], args[2]));

                if (Length(normal) < 1E-12)
                {
                    throw new CadCalcException("The three points defining the plane are coincident or collinear");
                }

                return Unit(true, normal);
            }

            private static CadCalcValue DistanceToLine(List<CadCalcValue> args)
            {
                CadCalcValue way = Between(args[1], args[2]);
                double length = Length(way);

                if (length < 1E-12)
                {
                    throw new CadCalcException("The two points defining the line coincide");
                }

                return Checked(Length(Cross(Between(args[1], args[0]), way)) / length);
            }

            private static CadCalcValue DistanceToPlane(List<CadCalcValue> args)
            {
                CadCalcValue normal = Cross(Between(args[1], args[2]), Between(args[1], args[3]));
                double length = Length(normal);

                if (length < 1E-12)
                {
                    throw new CadCalcException("The three points defining the plane are coincident or collinear");
                }

                return Checked(Math.Abs(Dot(Between(args[1], args[0]), normal)) / length);
            }

            private static CadCalcValue LinePlane(List<CadCalcValue> args)
            {
                CadCalcValue normal = Cross(Between(args[2], args[3]), Between(args[2], args[4]));

                if (Length(normal) < 1E-12)
                {
                    throw new CadCalcException("The three points defining the plane are coincident or collinear");
                }

                CadCalcValue way = Between(args[0], args[1]);
                double along = Dot(way, normal);

                if (Math.Abs(along) < 1E-12 * Length(way) * Length(normal))
                {
                    throw new CadCalcException("The line and the plane are parallel");
                }

                double t = Dot(Between(args[0], args[2]), normal) / along;
                return CadCalcValue.Vector(args[0].X + t * way.X, args[0].Y + t * way.Y, args[0].Z + t * way.Z);
            }

            // ang(v), ang(p1,p2): the angle of a 2D line from the X axis; ang(apex,p1,p2): the angle between two lines at an apex;
            // ang(apex,p1,p2,normal): the same counted anticlockwise about a normal.
            private static CadCalcValue Angle(List<CadCalcValue> args)
            {
                if (args.Count == 3 || args.Count == 4)
                {
                    Points("ANG", args, args.Count);
                    CadCalcValue a = Between(args[0], args[1]);
                    CadCalcValue b = Between(args[0], args[2]);

                    if (Length(a) < 1E-12 || Length(b) < 1E-12)
                    {
                        throw new CadCalcException("The two points defining a line in function ANG coincide");
                    }

                    double cos = Dot(a, b);
                    double degrees;

                    if (args.Count == 3)
                    {
                        degrees = Math.Atan2(Length(Cross(a, b)), cos) * 180.0 / Math.PI;
                    }
                    else
                    {
                        CadCalcValue n = Unit(true, args[3]);
                        degrees = Math.Atan2(Dot(Cross(a, b), n), cos) * 180.0 / Math.PI;
                        degrees = degrees < 0.0 ? degrees + 360.0 : degrees;
                    }

                    return CadCalcValue.Real(degrees);
                }

                double dx;
                double dy;

                if (args.Count == 1)
                {
                    Points("ANG", args, 1);
                    dx = args[0].X;
                    dy = args[0].Y;

                    if (Math.Abs(dx) < 1E-12 && Math.Abs(dy) < 1E-12)
                    {
                        throw new CadCalcException("The 2D vector in ANG(v) function is zero length");
                    }
                }
                else
                {
                    Points("ANG", args, 2);
                    dx = args[1].X - args[0].X;
                    dy = args[1].Y - args[0].Y;

                    if (Math.Abs(dx) < 1E-12 && Math.Abs(dy) < 1E-12)
                    {
                        throw new CadCalcException("The 2D points in ANG(p1,p2) function coincide");
                    }
                }

                double angle = Math.Atan2(dy, dx) * 180.0 / Math.PI;
                return CadCalcValue.Real(angle < 0.0 ? angle + 360.0 : angle);
            }

            private static CadCalcValue Intersection(List<CadCalcValue> args)
            {
                GeoPoint3 a = args[0].Point;
                GeoPoint3 b = args[1].Point;
                GeoPoint3 c = args[2].Point;
                GeoPoint3 d = args[3].Point;
                double rx = b.X - a.X;
                double ry = b.Y - a.Y;
                double sx = d.X - c.X;
                double sy = d.Y - c.Y;

                if ((Math.Abs(rx) < 1E-12 && Math.Abs(ry) < 1E-12) || (Math.Abs(sx) < 1E-12 && Math.Abs(sy) < 1E-12))
                {
                    throw new CadCalcException("The two points defining line in function ILL coincide");
                }

                double cross = rx * sy - ry * sx;

                if (Math.Abs(cross) < 1E-12 * Math.Max(1.0, Math.Abs(rx * sx + ry * sy)))
                {
                    throw new CadCalcException("The two lines in function ILL do not intersect or are collinear");
                }

                double t = ((c.X - a.X) * sy - (c.Y - a.Y) * sx) / cross;
                return CadCalcValue.Vector(a.X + t * rx, a.Y + t * ry, a.Z + t * (b.Z - a.Z));
            }

            private static CadCalcValue Real(string name, CadCalcValue arg)
            {
                double x = arg.Number;
                const double ToRadians = Math.PI / 180.0;

                switch (name)
                {
                    case "SIN":
                        return Checked(Math.Sin(x * ToRadians));
                    case "COS":
                        return Checked(Math.Cos(x * ToRadians));
                    case "TANG":
                        if (Math.Abs(Math.IEEERemainder(x - 90.0, 180.0)) < 1E-12)
                        {
                            throw new CadCalcException("Function TANG does not accept arguments 90+k*180 degrees");
                        }

                        return Checked(Math.Tan(x * ToRadians));
                    case "ASIN":
                    case "ACOS":
                        if (x < -1.0 || x > 1.0)
                        {
                            throw new CadCalcException("Argument to ASIN, ACOS must be between -1 and 1");
                        }

                        return Checked((name == "ASIN" ? Math.Asin(x) : Math.Acos(x)) / ToRadians);
                    case "ATAN":
                        return Checked(Math.Atan(x) / ToRadians);
                    case "LN":
                    case "LOG":
                        if (x <= 0.0)
                        {
                            throw new CadCalcException("Function " + name + " requires positive argument");
                        }

                        return Checked(name == "LN" ? Math.Log(x) : Math.Log10(x));
                    case "EXP":
                        return Checked(Math.Exp(x));
                    case "EXP10":
                        return Checked(Math.Pow(10.0, x));
                    case "SQR":
                        return arg.IsInteger ? CadCalcValue.Integer((long)(x * x)) : Checked(x * x);
                    case "SQRT":
                        if (x < 0.0)
                        {
                            throw new CadCalcException("Argument to SQRT cannot be negative");
                        }

                        return Checked(Math.Sqrt(x));
                    case "ABS":
                        return arg.IsInteger ? CadCalcValue.Integer((long)Math.Abs(x)) : CadCalcValue.Real(Math.Abs(x));
                    case "ROUND":
                    case "TRUNC":
                        double whole =
                            name == "ROUND" ? Math.Floor(x + 0.5)
                            : x < 0.0 ? Math.Ceiling(x)
                            : Math.Floor(x);

                        if (whole > int.MaxValue || whole < int.MinValue)
                        {
                            throw new CadCalcException(
                                "Argument to ROUND and TRUNC must be between 2147483647 and -2147483648"
                            );
                        }

                        return CadCalcValue.Integer((long)whole);
                    case "R2D":
                        return Checked(x / ToRadians);
                    default:
                        return Checked(x * ToRadians);
                }
            }

            private static CadCalcValue Add(CadCalcValue a, CadCalcValue b, double sign)
            {
                if (a.IsVector != b.IsVector)
                {
                    throw new CadCalcException("Operators '+', '-' require operands of the same type");
                }

                if (a.IsVector)
                {
                    return CadCalcValue.Vector(a.X + sign * b.X, a.Y + sign * b.Y, a.Z + sign * b.Z);
                }

                if (a.IsInteger && b.IsInteger)
                {
                    return Whole(a.X + sign * b.X);
                }

                return Checked(a.X + sign * b.X);
            }

            private static CadCalcValue Multiply(CadCalcValue a, CadCalcValue b)
            {
                if (a.IsVector && b.IsVector)
                {
                    return Checked(a.X * b.X + a.Y * b.Y + a.Z * b.Z);
                }

                if (a.IsVector || b.IsVector)
                {
                    CadCalcValue v = a.IsVector ? a : b;
                    double k = a.IsVector ? b.Number : a.Number;
                    return CadCalcValue.Vector(v.X * k, v.Y * k, v.Z * k);
                }

                return a.IsInteger && b.IsInteger ? Whole(a.X * b.X) : Checked(a.X * b.X);
            }

            private static CadCalcValue Divide(CadCalcValue a, CadCalcValue b)
            {
                if (b.IsVector)
                {
                    throw new CadCalcException("Operator '/' requires real-real or vector-real operands");
                }

                if (b.Number == 0.0)
                {
                    throw new CadCalcException("Division by 0");
                }

                if (a.IsVector)
                {
                    return CadCalcValue.Vector(a.X / b.Number, a.Y / b.Number, a.Z / b.Number);
                }

                return Checked(a.X / b.Number);
            }

            private static CadCalcValue Whole(double value) =>
                value > int.MaxValue || value < int.MinValue ? Checked(value) : CadCalcValue.Integer((long)value);

            private static CadCalcValue Checked(double value)
            {
                if (double.IsNaN(value) || double.IsInfinity(value))
                {
                    throw new CadCalcException("Numeric overflow");
                }

                return CadCalcValue.Real(value);
            }
        }
    }
}
