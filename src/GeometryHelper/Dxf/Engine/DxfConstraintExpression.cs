using System;
using System.Globalization;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The expression of a variable of a block's constraints (AcDbCalc), as AutoCAD's Parameters Manager writes it: numbers,
    /// the names of other variables (letters of either case the same), + - * / ^ and brackets, a sign before a term; so the
    /// samples' <c>width-W3*2</c>, <c>Length-30</c> and <c>HdCr/4</c>; and the functions sin, cos, tan, asin, acos, atan,
    /// sqrt, abs and round, their angles in degrees as AutoCAD 2021 read <c>width*cos(60)</c> as half the width (plugin
    /// D27b). Any other function, or anything else, is refused. A value is whole or real as AutoCAD 2021 kept it (plugin D27b):
    /// a number written without a point or an exponent is whole, a sum, a difference or a product of whole values is whole,
    /// and a quotient or a power is real, as it kept <c>Length-30</c> whole and <c>DIA/2</c> real.
    /// </summary>
    internal static class DxfConstraintExpression
    {
        /// <summary>The value of an expression, each name read by the function given.</summary>
        /// <exception cref="NotSupportedException">Thrown when the expression holds anything but numbers, names, + - * / ^, brackets and those functions, or cannot be read whole.</exception>
        internal static double Evaluate(string expression, Func<string, double> variable)
            => Evaluate(expression, variable, name => false, out _);

        /// <summary>
        /// The value of an expression, each name read by the function given and said whole or not by the other; whether the
        /// value is whole, as <see cref="DxfConstraintExpression"/> says.
        /// </summary>
        /// <exception cref="NotSupportedException">Thrown when the expression holds anything but numbers, names, + - * / ^, brackets and those functions, or cannot be read whole.</exception>
        internal static double Evaluate(string expression, Func<string, double> variable, Func<string, bool> whole, out bool isWhole)
        {
            var reader = new Reader { Text = expression ?? string.Empty, Variable = variable, Whole = whole };
            Term value = reader.Sum();
            reader.Blanks();

            if (reader.At != reader.Text.Length || double.IsNaN(value.Value) || double.IsInfinity(value.Value))
            {
                throw Refused(expression);
            }

            isWhole = value.Whole;
            return value.Value;
        }

        private static NotSupportedException Refused(string expression)
            => new NotSupportedException("The expression " + expression + " of a block's constraints holds what the library does not evaluate: it evaluates numbers, the names of variables, + - * / ^, brackets and the functions sin, cos, tan, asin, acos, atan, sqrt, abs and round.");

        /// <summary>A value worked out so far, and whether it is whole.</summary>
        private readonly struct Term
        {
            internal Term(double value, bool whole)
            {
                Value = value;
                Whole = whole;
            }

            internal double Value { get; }

            internal bool Whole { get; }
        }

        private sealed class Reader
        {
            internal string Text { get; set; }

            internal Func<string, double> Variable { get; set; }

            internal Func<string, bool> Whole { get; set; }

            internal int At { get; set; }

            internal void Blanks()
            {
                while (At < Text.Length && char.IsWhiteSpace(Text[At]))
                {
                    At++;
                }
            }

            private bool Take(char c)
            {
                Blanks();

                if (At < Text.Length && Text[At] == c)
                {
                    At++;
                    return true;
                }

                return false;
            }

            internal Term Sum()
            {
                Term value = Product();

                while (true)
                {
                    if (Take('+'))
                    {
                        Term next = Product();
                        value = new Term(value.Value + next.Value, value.Whole && next.Whole);
                    }
                    else if (Take('-'))
                    {
                        Term next = Product();
                        value = new Term(value.Value - next.Value, value.Whole && next.Whole);
                    }
                    else
                    {
                        return value;
                    }
                }
            }

            private Term Product()
            {
                Term value = Signed();

                while (true)
                {
                    if (Take('*'))
                    {
                        Term next = Signed();
                        value = new Term(value.Value * next.Value, value.Whole && next.Whole);
                    }
                    else if (Take('/'))
                    {
                        value = new Term(value.Value / Signed().Value, false);
                    }
                    else
                    {
                        return value;
                    }
                }
            }

            // A sign binds looser than a power: -2^2 is -4.
            private Term Signed()
            {
                if (Take('-'))
                {
                    Term term = Signed();
                    return new Term(-term.Value, term.Whole);
                }

                return Take('+') ? Signed() : Power();
            }

            private Term Power()
            {
                Term value = Primary();
                return Take('^') ? new Term(Math.Pow(value.Value, Signed().Value), false) : value;
            }

            private Term Primary()
            {
                Blanks();

                if (Take('('))
                {
                    Term inner = Sum();

                    if (!Take(')'))
                    {
                        throw Refused(Text);
                    }

                    return inner;
                }

                int from = At;

                if (At < Text.Length && (char.IsDigit(Text[At]) || Text[At] == '.'))
                {
                    while (At < Text.Length && (char.IsDigit(Text[At]) || Text[At] == '.'))
                    {
                        At++;
                    }

                    if (At < Text.Length && (Text[At] == 'e' || Text[At] == 'E'))
                    {
                        int mark = At++;

                        if (At < Text.Length && (Text[At] == '+' || Text[At] == '-'))
                        {
                            At++;
                        }

                        if (At < Text.Length && char.IsDigit(Text[At]))
                        {
                            while (At < Text.Length && char.IsDigit(Text[At]))
                            {
                                At++;
                            }
                        }
                        else
                        {
                            At = mark;
                        }
                    }

                    string written = Text.Substring(from, At - from);

                    if (!double.TryParse(written, NumberStyles.Float, CultureInfo.InvariantCulture, out double number))
                    {
                        throw Refused(Text);
                    }

                    return new Term(number, written.IndexOfAny(new[] { '.', 'e', 'E' }) < 0);
                }

                while (At < Text.Length && (char.IsLetterOrDigit(Text[At]) || Text[At] == '_'))
                {
                    At++;
                }

                if (At == from)
                {
                    throw Refused(Text);
                }

                string name = Text.Substring(from, At - from);
                Blanks();

                if (At < Text.Length && Text[At] == '(')
                {
                    At++;
                    Term argument = Sum();

                    if (!Take(')'))
                    {
                        throw Refused(Text);
                    }

                    return Function(name, argument);
                }

                return new Term(Variable(name), Whole(name));
            }

            /// <summary>
            /// A function of one value, as AutoCAD 2021 worked them out (plugin D27b): sin, cos and tan of degrees, asin, acos
            /// and atan in degrees and sqrt, each real; abs, of the value's kind; round, to the nearer whole number, halves away
            /// from nought, whole. Any other is refused.
            /// </summary>
            private Term Function(string name, Term argument)
            {
                const double Degree = Math.PI / 180.0;
                double v = argument.Value;

                switch (name.ToLowerInvariant())
                {
                    case "sin":
                        return new Term(Math.Sin(v * Degree), false);
                    case "cos":
                        return new Term(Math.Cos(v * Degree), false);
                    case "tan":
                        return new Term(Math.Tan(v * Degree), false);
                    case "asin":
                        return new Term(Math.Asin(v) / Degree, false);
                    case "acos":
                        return new Term(Math.Acos(v) / Degree, false);
                    case "atan":
                        return new Term(Math.Atan(v) / Degree, false);
                    case "sqrt":
                        return new Term(Math.Sqrt(v), false);
                    case "abs":
                        return new Term(Math.Abs(v), argument.Whole);
                    case "round":
                        return new Term(Math.Round(v, MidpointRounding.AwayFromZero), true);
                    default:
                        throw Refused(Text);
                }
            }
        }
    }
}
