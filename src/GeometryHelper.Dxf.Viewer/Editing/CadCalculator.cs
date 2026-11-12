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
        /// Writes the value as CAL writes it: a whole number as 7, a real number to six significant digits as AutoLISP does
        /// (4.0, 1.41421, 53.1301), a vector as (1.0 2.0 0.0).
        /// </summary>
        /// <returns>The text.</returns>
        public override string ToString()
        {
            if (IsVector)
            {
                return "(" + Write(X) + " " + Write(Y) + " " + Write(Z) + ")";
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

        private static string Write(double value)
        {
            if (Math.Abs(value) < 1E-12)
            {
                return "0.0";
            }

            double magnitude = Math.Abs(value);

            if (magnitude >= 1E6 || magnitude < 1E-4)
            {
                return value.ToString("0.#####e+000", CultureInfo.InvariantCulture);
            }

            int whole = magnitude < 1.0 ? 0 : (int)Math.Floor(Math.Log10(magnitude)) + 1;
            string text = Math.Round(value, Math.Max(0, 6 - whole)).ToString("0.######", CultureInfo.InvariantCulture);
            return text.IndexOf('.') < 0 ? text + ".0" : text;
        }
    }

    /// <summary>A mistake in an expression of the geometry calculator, its message in AutoCAD 2021's words.</summary>
    public sealed class CadCalcException : Exception
    {
        /// <summary>Makes the mistake.</summary>
        /// <param name="message">What AutoCAD says: "Division by 0", "Function FOO doesn't exist".</param>
        public CadCalcException(string message)
            : base(message) { }
    }

    /// <summary>
    /// The geometry calculator of CAL and QuickCalc, as AutoCAD 2021's: numbers, vectors written <c>[x,y,z]</c> (a missing
    /// coordinate nought), <c>+ - * / ^</c> and brackets, the functions <c>sin cos tang asin acos atan</c> (in degrees),
    /// <c>ln log exp exp10 sqr sqrt abs round trunc r2d d2r</c>, the constant <c>pi</c>, and of points <c>dist(p1,p2)</c>,
    /// <c>ang(p1,p2)</c> or <c>ang(v)</c>, <c>ill(p1,p2,p3,p4)</c> and <c>cur</c>, a point picked in the drawing.
    /// </summary>
    /// <remarks>
    /// <c>1+2*3</c> is 7, <c>sqrt(16)+2</c> 6.0, <c>dist([0,0],[300,400])</c> 500.0, <c>ang([0,0],[300,400])</c> 53.1301 and
    /// <c>[100,50]+[20,30]</c> (120.0 80.0 0.0). A number added to a vector is refused as AutoCAD refuses it: "Operators '+',
    /// '-' require operands of the same type".
    /// </remarks>
    public static class CadCalculator
    {
        private static readonly HashSet<string> RealFunctions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "SIN",
            "COS",
            "TANG",
            "TAN",
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

        /// <summary>Counts the points an expression picks in the drawing: each <c>cur</c> in it, as CAL asks one "&gt;&gt; Enter a point:" for each.</summary>
        /// <param name="expression">The expression.</param>
        /// <returns>How many.</returns>
        public static int PointsPicked(string expression)
        {
            int count = 0;

            foreach (string name in Names(expression ?? string.Empty))
            {
                if (string.Equals(name, "CUR", StringComparison.OrdinalIgnoreCase))
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>Evaluates an expression.</summary>
        /// <param name="expression">The expression.</param>
        /// <param name="picked">The points <c>cur</c> takes, in the order the expression names them; null where it names none.</param>
        /// <returns>The value.</returns>
        /// <exception cref="CadCalcException">Thrown when the expression is wrong, in AutoCAD's words.</exception>
        public static CadCalcValue Evaluate(string expression, IReadOnlyList<GeoPoint3> picked = null)
        {
            var parser = new Parser(expression ?? string.Empty, picked);
            CadCalcValue value = parser.Expression();
            parser.SkipSpaces();

            if (!parser.AtEnd)
            {
                throw new CadCalcException(
                    parser.Current == ')' ? "Wrong syntax of expression" : "Illegal character '" + parser.Current + "'"
                );
            }

            return value;
        }

        private static IEnumerable<string> Names(string text)
        {
            var name = new StringBuilder();

            foreach (char c in text + " ")
            {
                if (char.IsLetter(c) || (name.Length > 0 && (char.IsDigit(c) || c == '_')))
                {
                    name.Append(c);
                }
                else
                {
                    if (name.Length > 0)
                    {
                        yield return name.ToString();
                    }

                    name.Clear();
                }
            }
        }

        private sealed class Parser
        {
            private readonly string _text;
            private readonly IReadOnlyList<GeoPoint3> _picked;
            private int _at;
            private int _pick;

            public Parser(string text, IReadOnlyList<GeoPoint3> picked)
            {
                _text = text;
                _picked = picked;
            }

            public bool AtEnd => _at >= _text.Length;

            public char Current => _text[_at];

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
                        value = Add(value, Term(), 1.0);
                    }
                    else if (Take('-'))
                    {
                        value = Add(value, Term(), -1.0);
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
                        value = Multiply(value, Power());
                    }
                    else if (Take('/'))
                    {
                        value = Divide(value, Power());
                    }
                    else
                    {
                        return value;
                    }
                }
            }

            private CadCalcValue Power()
            {
                CadCalcValue value = Unary();

                if (!Take('^'))
                {
                    return value;
                }

                CadCalcValue exponent = Power();

                if (value.IsVector || exponent.IsVector)
                {
                    throw new CadCalcException("Power operator '^' requires real operands");
                }

                if (value.Number < 0.0 && !exponent.IsInteger)
                {
                    throw new CadCalcException("Negative numbers cannot be raised to real exponent");
                }

                return Checked(Math.Pow(value.Number, exponent.Number));
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
                    switch (upper)
                    {
                        case "PI":
                            return CadCalcValue.Real(Math.PI);
                        case "CUR":
                            if (_picked == null || _pick >= _picked.Count)
                            {
                                throw new CadCalcException("No object picked.");
                            }

                            GeoPoint3 p = _picked[_pick++];
                            return CadCalcValue.Vector(p.X, p.Y, p.Z);
                        default:
                            throw new CadCalcException(
                                RealFunctions.Contains(upper) || IsPointFunction(upper)
                                    ? "Function " + upper + " requires one argument"
                                    : "AutoLISP variable " + name + " has no value"
                            );
                    }
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

                return Call(upper, args);
            }

            private static bool IsPointFunction(string name) => name == "DIST" || name == "ANG" || name == "ILL";

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
                    default:
                        throw new CadCalcException("Function " + name + " doesn't exist");
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

            private static CadCalcValue Angle(List<CadCalcValue> args)
            {
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

                double degrees = Math.Atan2(dy, dx) * 180.0 / Math.PI;
                return CadCalcValue.Real(degrees < 0.0 ? degrees + 360.0 : degrees);
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
                    case "TAN":
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
