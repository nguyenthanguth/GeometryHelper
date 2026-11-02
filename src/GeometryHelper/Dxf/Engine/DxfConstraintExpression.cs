using System;
using System.Globalization;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The expression of a variable of a block's constraints (AcDbCalc), as AutoCAD's Parameters Manager writes it: numbers,
    /// the names of other variables (letters of either case the same), + - * / % ^ and brackets, a sign before a term; so the
    /// samples' <c>width-W3*2</c>, <c>Length-30</c> and <c>HdCr/4</c>; the constants pi and e, which AutoCAD 2021 read
    /// before a variable of the same name; and the functions AutoCAD's help lists but random, as AutoCAD 2021 worked them out
    /// (plugins D27b, D34): sin, cos, tan, asin, acos and atan, their angles in degrees as it read <c>width*cos(60)</c> as
    /// half the width; sinh, cosh, tanh, asinh, acosh, atanh, sqrt, exp, exp10, ln, log (of base 10), d2r and r2d; abs and
    /// sign; round, halves up as it gave round(-2.5) -2 and round(2.5) 3, trunc, floor and ceil; and min, max and pow of two
    /// values apart by a semicolon or a comma. Any other function, random, which AutoCAD draws anew each time, or anything
    /// else, is refused, and so is what AutoCAD 2021 would not work out: a root, a logarithm or an inverse of a value out of
    /// its range, a division or a remainder by nought, a remainder of a value not whole, and a round, trunc, floor or ceil
    /// past a whole number of 32 bits. A value is whole or real as AutoCAD 2021 kept it (plugins D27b, D34): a number
    /// written without a point or an exponent is whole, a sum, a difference or a product of whole values is whole, and a
    /// quotient, a power, pi and e are real, as it kept <c>Length-30</c> whole and <c>DIA/2</c> real; a remainder, round,
    /// trunc, floor, ceil and sign are whole, abs of the value's kind, min and max of the kind of the value they give, the
    /// first where the two are equal, and every other function real; and a whole value past 32 bits is real, as it kept
    /// 2147483647+1 as the real 2147483648.
    /// </summary>
    internal static class DxfConstraintExpression
    {
        /// <summary>The value of an expression, each name read by the function given.</summary>
        /// <exception cref="NotSupportedException">Thrown when the expression holds anything but numbers, names, + - * / % ^, brackets, pi, e and those functions, cannot be read whole, or gives what AutoCAD would not work out.</exception>
        internal static double Evaluate(string expression, Func<string, double> variable)
            => Evaluate(expression, variable, name => false, out _);

        /// <summary>
        /// The value of an expression, each name read by the function given and said whole or not by the other; whether the
        /// value is whole, as <see cref="DxfConstraintExpression"/> says.
        /// </summary>
        /// <exception cref="NotSupportedException">Thrown when the expression holds anything but numbers, names, + - * / % ^, brackets, pi, e and those functions, cannot be read whole, or gives what AutoCAD would not work out.</exception>
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
            => new NotSupportedException("The expression " + expression + " of a block's constraints holds what the library does not evaluate, or gives what AutoCAD would not work out: it evaluates numbers, the names of variables, + - * / % ^, brackets, pi, e and the functions sin, cos, tan, asin, acos, atan, sinh, cosh, tanh, asinh, acosh, atanh, sqrt, exp, exp10, ln, log, d2r, r2d, abs, sign, round, trunc, floor, ceil, min, max and pow.");

        /// <summary>A value worked out so far, and whether it is whole: never where it is past a whole number of 32 bits.</summary>
        private readonly struct Term
        {
            internal Term(double value, bool whole)
            {
                Value = value;
                Whole = whole && InRange(value);
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
                        // A division by nought is refused where it is made, as AutoCAD 2021 refused 1/0 (plugin D34).
                        Term by = Signed();

                        if (by.Value == 0.0)
                        {
                            throw Refused(Text);
                        }

                        value = new Term(value.Value / by.Value, false);
                    }
                    else if (Take('%'))
                    {
                        value = Remainder(value, Signed());
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
                    Term? second = Take(';') || Take(',') ? Sum() : (Term?)null;

                    if (!Take(')'))
                    {
                        throw Refused(Text);
                    }

                    return Function(name.ToLowerInvariant(), argument, second);
                }

                // The constants come before a variable of the same name, as AutoCAD 2021 read pi*1 as pi beside a variable pi
                // of 5 (plugin D34); random, which it also takes without brackets, is refused.
                switch (name.ToLowerInvariant())
                {
                    case "pi":
                        return new Term(Math.PI, false);
                    case "e":
                        return new Term(Math.E, false);
                    case "random":
                        throw Refused(Text);
                }

                return new Term(Variable(name), Whole(name));
            }

            /// <summary>
            /// The remainder of a whole division, of the sign of the value divided, whole, as AutoCAD 2021 gave 7%-3 1 and
            /// -7%3 -1; refused of a value not whole, as it refused 7.5%2 but took 7.0%2, or by nought (plugin D34).
            /// </summary>
            private Term Remainder(Term value, Term by)
            {
                if (Math.Floor(value.Value) != value.Value || Math.Floor(by.Value) != by.Value || !InRange(value.Value) || !InRange(by.Value) || by.Value == 0.0)
                {
                    throw Refused(Text);
                }

                return new Term(value.Value % by.Value, true);
            }

            /// <summary>
            /// A function, by its name in lower case, of the value given, or of the two given to min, max and pow, as
            /// <see cref="DxfConstraintExpression"/> says; refused where it is no such function, is given one value where it
            /// takes two or two where it takes one, or gives what is no number.
            /// </summary>
            private Term Function(string name, Term argument, Term? second)
            {
                Term worked = second.HasValue ? Two(name, argument, second.Value) : One(name, argument);

                if (double.IsNaN(worked.Value) || double.IsInfinity(worked.Value))
                {
                    throw Refused(Text);
                }

                return worked;
            }

            /// <summary>
            /// A function of one value, as AutoCAD 2021 worked them out (plugins D27b, D34): sin, cos and tan of degrees, asin,
            /// acos and atan in degrees, the rest of radians, each real; abs, of the value's kind, and sign, whole; round to the
            /// whole number at or below the value and a half, as it gave round(-0.5) 0, trunc toward nought, floor and ceil,
            /// each whole and within a whole number of 32 bits, as it refused round(3000000000.5) and gave
            /// round(2147483647.4).
            /// </summary>
            private Term One(string name, Term argument)
            {
                const double Degree = Math.PI / 180.0;
                double v = argument.Value;

                switch (name)
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
                    case "sinh":
                        return new Term(Math.Sinh(v), false);
                    case "cosh":
                        return new Term(Math.Cosh(v), false);
                    case "tanh":
                        return new Term(Math.Tanh(v), false);
                    case "asinh":
                        return new Term(Math.Log(v + Math.Sqrt(v * v + 1.0)), false);
                    case "acosh":
                        return new Term(v < 1.0 ? double.NaN : Math.Log(v + Math.Sqrt(v * v - 1.0)), false);
                    case "atanh":
                        return new Term(Math.Abs(v) >= 1.0 ? double.NaN : 0.5 * Math.Log((1.0 + v) / (1.0 - v)), false);
                    case "sqrt":
                        return new Term(Math.Sqrt(v), false);
                    case "exp":
                        return new Term(Math.Exp(v), false);
                    case "exp10":
                        return new Term(Math.Exp(v * Math.Log(10.0)), false);
                    case "ln":
                        return new Term(Math.Log(v), false);
                    case "log":
                        return new Term(Math.Log(v) / Math.Log(10.0), false);
                    case "d2r":
                        return new Term(v * Degree, false);
                    case "r2d":
                        return new Term(v / Degree, false);
                    case "abs":
                        return new Term(Math.Abs(v), argument.Whole);
                    case "sign":
                        return new Term(Math.Sign(v), true);
                    case "round":
                        return new Term(Within(Math.Floor(v + 0.5)), true);
                    case "trunc":
                        return new Term(Within(Math.Truncate(v)), true);
                    case "floor":
                        return new Term(Within(Math.Floor(v)), true);
                    case "ceil":
                        return new Term(Within(Math.Ceiling(v)), true);
                    default:
                        throw Refused(Text);
                }
            }

            /// <summary>
            /// A function of two values: min and max the one they give, of its kind, the first where the two are equal, as
            /// AutoCAD 2021 gave max(2;2.0) the whole 2 and max(2.0;2) the real; pow the first to the power of the second,
            /// real (plugin D34).
            /// </summary>
            private Term Two(string name, Term first, Term second)
            {
                switch (name)
                {
                    case "min":
                        return second.Value < first.Value ? second : first;
                    case "max":
                        return second.Value > first.Value ? second : first;
                    case "pow":
                        return new Term(Math.Pow(first.Value, second.Value), false);
                    default:
                        throw Refused(Text);
                }
            }

            /// <summary>A whole number a value is taken to; NaN where it is past a whole number of 32 bits.</summary>
            private static double Within(double whole) => InRange(whole) ? whole : double.NaN;
        }

        /// <summary>Whether a value is within a whole number of 32 bits, as AutoCAD 2021 keeps a whole value.</summary>
        private static bool InRange(double value) => value >= int.MinValue && value <= int.MaxValue;
    }
}
