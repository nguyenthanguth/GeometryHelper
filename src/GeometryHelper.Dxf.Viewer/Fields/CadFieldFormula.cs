using System;
using System.Globalization;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The formula of an AcExpr field, as AutoCAD 2021 works one out (checked in its console): numbers, brackets, + - * / ^ and
    /// the functions sqrt, abs, sin, cos, tan, atan, exp, ln, log and the constant pi, the angles in degrees. A result of whole
    /// numbers that is whole is a whole number (<c>2*3+1</c> is 7); any other is a real (<c>(3+4)/2</c> is 3.5).
    /// </summary>
    internal sealed class CadFieldFormula
    {
        private readonly string _text;
        private int _at;
        private bool _real;

        private CadFieldFormula(string text)
        {
            _text = text ?? string.Empty;
        }

        /// <summary>Works a formula out.</summary>
        /// <param name="formula">The formula.</param>
        /// <param name="value">The value.</param>
        /// <param name="isWhole">Whether every number in it is whole and so is the value.</param>
        /// <returns>False where the formula is not one.</returns>
        internal static bool TryEvaluate(string formula, out double value, out bool isWhole)
        {
            var reader = new CadFieldFormula(formula);

            try
            {
                value = reader.Sum();
                reader.Skip();
                bool ok = reader._at >= reader._text.Length && !double.IsNaN(value) && !double.IsInfinity(value);
                isWhole = ok && !reader._real && Math.Abs(value - Math.Round(value)) < 1E-9;
                return ok;
            }
            catch (FormatException)
            {
                value = 0.0;
                isWhole = false;
                return false;
            }
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
            double total = Product();

            while (true)
            {
                if (Take('+'))
                {
                    total += Product();
                }
                else if (Take('-'))
                {
                    total -= Product();
                }
                else
                {
                    return total;
                }
            }
        }

        private double Product()
        {
            double total = Power();

            while (true)
            {
                if (Take('*'))
                {
                    total *= Power();
                }
                else if (Take('/'))
                {
                    double divisor = Power();

                    if (divisor == 0.0)
                    {
                        throw new FormatException("A formula divides by nought.");
                    }

                    // A quotient that is not whole is a real, 7/2 as 3.5; one that is whole stays whole, 8/2 as 4.
                    double quotient = total / divisor;

                    if (Math.Abs(quotient - Math.Round(quotient)) > 1E-9)
                    {
                        _real = true;
                    }

                    total = quotient;
                }
                else
                {
                    return total;
                }
            }
        }

        private double Power()
        {
            double value = Unary();

            if (Take('^'))
            {
                _real = true;
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

            if (Take('('))
            {
                double inside = Sum();

                if (!Take(')'))
                {
                    throw new FormatException("A bracket of a formula is not closed.");
                }

                return inside;
            }

            int start = _at;

            if (_at < _text.Length && char.IsLetter(_text[_at]))
            {
                while (_at < _text.Length && char.IsLetterOrDigit(_text[_at]))
                {
                    _at++;
                }

                string name = _text.Substring(start, _at - start).ToLowerInvariant();

                if (name == "pi")
                {
                    _real = true;
                    return Math.PI;
                }

                if (!Take('('))
                {
                    throw new FormatException("A formula names " + name + ", which is not a function.");
                }

                double argument = Sum();

                if (!Take(')'))
                {
                    throw new FormatException("A bracket of a formula is not closed.");
                }

                _real = true;
                return Function(name, argument);
            }

            while (_at < _text.Length && (char.IsDigit(_text[_at]) || _text[_at] == '.'))
            {
                _at++;
            }

            if (
                _at == start
                || !double.TryParse(
                    _text.Substring(start, _at - start),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out double number
                )
            )
            {
                throw new FormatException("A formula expects a number.");
            }

            if (_text.IndexOf('.', start, _at - start) >= 0)
            {
                _real = true;
            }

            return number;
        }

        private static double Function(string name, double x)
        {
            switch (name)
            {
                case "sqrt":
                    return Math.Sqrt(x);
                case "abs":
                    return Math.Abs(x);
                case "sin":
                    return Math.Sin(x * Math.PI / 180.0);
                case "cos":
                    return Math.Cos(x * Math.PI / 180.0);
                case "tan":
                    return Math.Tan(x * Math.PI / 180.0);
                case "atan":
                    return Math.Atan(x) * 180.0 / Math.PI;
                case "exp":
                    return Math.Exp(x);
                case "ln":
                    return Math.Log(x);
                case "log":
                    return Math.Log10(x);
                default:
                    throw new FormatException("A formula names " + name + ", which is not a function.");
            }
        }
    }
}
