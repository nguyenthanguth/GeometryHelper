using System;
using System.Collections.Generic;
using System.Globalization;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// The expressions of parameters, as AutoCAD 2021's Parameters Manager takes them: numbers, the names of other parameters,
    /// the operators + - * / ^ and % (the remainder), brackets, and the functions sin, cos, tan, asin, acos, atan (angles in
    /// degrees), sqrt, abs, exp, ln, log (base 10), floor, ceil, round, min, max, pow and the constant pi. "d1*2+3" with d1 at
    /// 10 comes to 23.
    /// </summary>
    internal static class CadConstraintExpression
    {
        /// <summary>Works an expression out.</summary>
        /// <param name="expression">The expression.</param>
        /// <param name="lookup">The value of a name, or null for one that is none.</param>
        /// <param name="value">Gives the value.</param>
        /// <param name="error">Gives why not, where it cannot be worked out; null otherwise.</param>
        /// <returns>True where it came to a number.</returns>
        internal static bool TryEvaluate(
            string expression,
            Func<string, double?> lookup,
            out double value,
            out string error
        )
        {
            value = 0.0;
            error = null;

            if (string.IsNullOrWhiteSpace(expression))
            {
                error = "The expression is empty.";
                return false;
            }

            var parser = new Parser(expression, lookup);

            try
            {
                value = parser.Run();

                if (double.IsNaN(value) || double.IsInfinity(value))
                {
                    error = "The expression does not come to a number.";
                    return false;
                }

                return true;
            }
            catch (FormatException exception)
            {
                error = exception.Message;
                return false;
            }
        }

        /// <summary>The names an expression reads, in the order they first stand; empty where it does not parse.</summary>
        internal static List<string> NamesIn(string expression)
        {
            var names = new List<string>();

            if (string.IsNullOrWhiteSpace(expression))
            {
                return names;
            }

            var parser = new Parser(expression, name =>
            {
                if (!names.Exists(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase)))
                {
                    names.Add(name);
                }

                return 1.0;
            });

            try
            {
                parser.Run();
            }
            catch (FormatException)
            {
                return new List<string>();
            }

            return names;
        }

        /// <summary>Whether a text can be the name of a parameter: a letter or an underscore, then letters, digits and underscores; not a function or the constant.</summary>
        internal static bool IsName(string text)
        {
            if (string.IsNullOrEmpty(text) || !(char.IsLetter(text[0]) || text[0] == '_'))
            {
                return false;
            }

            foreach (char c in text)
            {
                if (!(char.IsLetterOrDigit(c) || c == '_'))
                {
                    return false;
                }
            }

            return !IsReserved(text);
        }

        private static bool IsReserved(string text)
        {
            switch (text.ToLowerInvariant())
            {
                case "pi":
                case "sin":
                case "cos":
                case "tan":
                case "asin":
                case "acos":
                case "atan":
                case "sqrt":
                case "abs":
                case "exp":
                case "ln":
                case "log":
                case "floor":
                case "ceil":
                case "round":
                case "min":
                case "max":
                case "pow":
                    return true;
                default:
                    return false;
            }
        }

        private sealed class Parser
        {
            private readonly string _text;
            private readonly Func<string, double?> _lookup;
            private int _at;

            internal Parser(string text, Func<string, double?> lookup)
            {
                _text = text;
                _lookup = lookup;
            }

            internal double Run()
            {
                double value = Sum();
                Skip();

                if (_at < _text.Length)
                {
                    throw new FormatException("Unexpected \"" + _text[_at] + "\" in the expression.");
                }

                return value;
            }

            private void Skip()
            {
                while (_at < _text.Length && char.IsWhiteSpace(_text[_at]))
                {
                    _at++;
                }
            }

            private bool Take(char c)
            {
                Skip();

                if (_at < _text.Length && _text[_at] == c)
                {
                    _at++;
                    return true;
                }

                return false;
            }

            private double Sum()
            {
                double value = Product();

                while (true)
                {
                    if (Take('+'))
                    {
                        value += Product();
                    }
                    else if (Take('-'))
                    {
                        value -= Product();
                    }
                    else
                    {
                        return value;
                    }
                }
            }

            private double Product()
            {
                double value = Power();

                while (true)
                {
                    if (Take('*'))
                    {
                        value *= Power();
                    }
                    else if (Take('/'))
                    {
                        double by = Power();

                        if (by == 0.0)
                        {
                            throw new FormatException("The expression divides by zero.");
                        }

                        value /= by;
                    }
                    else if (Take('%'))
                    {
                        double by = Power();

                        if (by == 0.0)
                        {
                            throw new FormatException("The expression divides by zero.");
                        }

                        value %= by;
                    }
                    else
                    {
                        return value;
                    }
                }
            }

            private double Power()
            {
                double value = Unary();

                if (Take('^'))
                {
                    // Right to left: 2^3^2 is 2^9.
                    return Math.Pow(value, Power());
                }

                return value;
            }

            private double Unary()
            {
                if (Take('-'))
                {
                    return -Unary();
                }

                if (Take('+'))
                {
                    return Unary();
                }

                return Atom();
            }

            private double Atom()
            {
                Skip();

                if (_at >= _text.Length)
                {
                    throw new FormatException("The expression ends too soon.");
                }

                char c = _text[_at];

                if (c == '(')
                {
                    _at++;
                    double inside = Sum();

                    if (!Take(')'))
                    {
                        throw new FormatException("A bracket of the expression is not closed.");
                    }

                    return inside;
                }

                if (char.IsDigit(c) || c == '.')
                {
                    int start = _at;

                    while (_at < _text.Length && (char.IsDigit(_text[_at]) || _text[_at] == '.'))
                    {
                        _at++;
                    }

                    if (
                        _at < _text.Length
                        && (_text[_at] == 'e' || _text[_at] == 'E')
                        && _at + 1 < _text.Length
                        && (char.IsDigit(_text[_at + 1]) || _text[_at + 1] == '-' || _text[_at + 1] == '+')
                    )
                    {
                        _at += 2;

                        while (_at < _text.Length && char.IsDigit(_text[_at]))
                        {
                            _at++;
                        }
                    }

                    if (
                        !double.TryParse(
                            _text.Substring(start, _at - start),
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out double number
                        )
                    )
                    {
                        throw new FormatException("\"" + _text.Substring(start, _at - start) + "\" is not a number.");
                    }

                    return number;
                }

                if (char.IsLetter(c) || c == '_')
                {
                    int start = _at;

                    while (_at < _text.Length && (char.IsLetterOrDigit(_text[_at]) || _text[_at] == '_'))
                    {
                        _at++;
                    }

                    string name = _text.Substring(start, _at - start);
                    Skip();

                    if (_at < _text.Length && _text[_at] == '(' && IsReserved(name))
                    {
                        _at++;
                        var arguments = new List<double> { Sum() };

                        while (Take(','))
                        {
                            arguments.Add(Sum());
                        }

                        if (!Take(')'))
                        {
                            throw new FormatException("A bracket of the expression is not closed.");
                        }

                        return Call(name.ToLowerInvariant(), arguments);
                    }

                    if (string.Equals(name, "pi", StringComparison.OrdinalIgnoreCase))
                    {
                        return Math.PI;
                    }

                    double? found = _lookup?.Invoke(name);

                    if (!found.HasValue)
                    {
                        throw new FormatException("\"" + name + "\" is not a parameter.");
                    }

                    return found.Value;
                }

                throw new FormatException("Unexpected \"" + c + "\" in the expression.");
            }

            private static double Call(string name, List<double> a)
            {
                const double Rad = Math.PI / 180.0;
                bool two = a.Count == 2;

                switch (name)
                {
                    case "sin" when a.Count == 1:
                        return Math.Sin(a[0] * Rad);
                    case "cos" when a.Count == 1:
                        return Math.Cos(a[0] * Rad);
                    case "tan" when a.Count == 1:
                        return Math.Tan(a[0] * Rad);
                    case "asin" when a.Count == 1:
                        return Math.Asin(a[0]) / Rad;
                    case "acos" when a.Count == 1:
                        return Math.Acos(a[0]) / Rad;
                    case "atan" when a.Count == 1:
                        return Math.Atan(a[0]) / Rad;
                    case "sqrt" when a.Count == 1:
                        return Math.Sqrt(a[0]);
                    case "abs" when a.Count == 1:
                        return Math.Abs(a[0]);
                    case "exp" when a.Count == 1:
                        return Math.Exp(a[0]);
                    case "ln" when a.Count == 1:
                        return Math.Log(a[0]);
                    case "log" when a.Count == 1:
                        return Math.Log10(a[0]);
                    case "floor" when a.Count == 1:
                        return Math.Floor(a[0]);
                    case "ceil" when a.Count == 1:
                        return Math.Ceiling(a[0]);
                    case "round" when a.Count == 1:
                        return Math.Round(a[0], MidpointRounding.AwayFromZero);
                    case "min" when two:
                        return Math.Min(a[0], a[1]);
                    case "max" when two:
                        return Math.Max(a[0], a[1]);
                    case "pow" when two:
                        return Math.Pow(a[0], a[1]);
                    default:
                        throw new FormatException("The function " + name + " is not given the arguments it takes.");
                }
            }
        }
    }
}
