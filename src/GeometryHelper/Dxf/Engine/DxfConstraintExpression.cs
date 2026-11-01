using System;
using System.Globalization;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The expression of a variable of a block's constraints (AcDbCalc), as AutoCAD's Parameters Manager writes it: numbers,
    /// the names of other variables (letters of either case the same), + - * / ^ and brackets, a sign before a term; so the
    /// samples' <c>width-W3*2</c>, <c>Length-30</c> and <c>HdCr/4</c>. A function, or anything else, is refused: the
    /// library was not shown how AutoCAD reads its angles.
    /// </summary>
    internal static class DxfConstraintExpression
    {
        /// <summary>The value of an expression, each name read by the function given.</summary>
        /// <exception cref="NotSupportedException">Thrown when the expression holds anything but numbers, names, + - * / ^ and brackets, or cannot be read whole.</exception>
        internal static double Evaluate(string expression, Func<string, double> variable)
        {
            var reader = new Reader { Text = expression ?? string.Empty, Variable = variable };
            double value = reader.Sum();
            reader.Blanks();

            if (reader.At != reader.Text.Length || double.IsNaN(value) || double.IsInfinity(value))
            {
                throw Refused(expression);
            }

            return value;
        }

        private static NotSupportedException Refused(string expression)
            => new NotSupportedException("The expression " + expression + " of a block's constraints holds what the library does not evaluate: it evaluates numbers, the names of variables, + - * / ^ and brackets.");

        private sealed class Reader
        {
            internal string Text { get; set; }

            internal Func<string, double> Variable { get; set; }

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

            internal double Sum()
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
                double value = Signed();

                while (true)
                {
                    if (Take('*'))
                    {
                        value *= Signed();
                    }
                    else if (Take('/'))
                    {
                        value /= Signed();
                    }
                    else
                    {
                        return value;
                    }
                }
            }

            // A sign binds looser than a power: -2^2 is -4.
            private double Signed()
            {
                if (Take('-'))
                {
                    return -Signed();
                }

                return Take('+') ? Signed() : Power();
            }

            private double Power()
            {
                double value = Primary();
                return Take('^') ? Math.Pow(value, Signed()) : value;
            }

            private double Primary()
            {
                Blanks();

                if (Take('('))
                {
                    double inner = Sum();

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

                    if (!double.TryParse(Text.Substring(from, At - from), NumberStyles.Float, CultureInfo.InvariantCulture, out double number))
                    {
                        throw Refused(Text);
                    }

                    return number;
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
                    throw Refused(Text);
                }

                return Variable(name);
            }
        }
    }
}
