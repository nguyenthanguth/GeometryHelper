using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The formula of a table cell, as AutoCAD 2021 reads one (<c>=Sum(A1:B3)</c>, <c>=A1+B2*2</c>): the arithmetic of
    /// <see cref="CadFieldFormula"/> (numbers, brackets, + - * / ^, sqrt, abs, sin, cos, tan, atan, exp, ln, log, pi), the
    /// references to cells by their column's letters and row's number (<c>B3</c>, <c>$B$3</c>), and the functions
    /// <c>Sum</c>, <c>Average</c> and <c>Count</c> of a range (<c>A1:B3</c>) or of cells and ranges beside each other
    /// (<c>Sum(A1:A3, C1)</c>). A cell that is empty counts nought in a sum and is left out of an average and a count; a cell of
    /// words that are no number makes the formula an error. The references of a formula copied one cell down move one row down,
    /// those with a <c>$</c> before the column or the row staying where they are.
    /// </summary>
    internal sealed class CadTableFormula
    {
        private static readonly Regex Reference = new Regex(@"^(\$?)([A-Za-z]{1,3})(\$?)(\d{1,6})$");

        private static readonly Regex Anywhere = new Regex(
            @"(?<![A-Za-z0-9_$])(\$?)([A-Za-z]{1,3})(\$?)(\d{1,6})(?![A-Za-z0-9_(])"
        );

        private readonly string _text;
        private readonly Func<int, int, double?> _cell;
        private readonly Func<int, int, bool> _holds;
        private int _at;
        private bool _real;

        private CadTableFormula(string text, Func<int, int, double?> cell, Func<int, int, bool> holds)
        {
            _text = text ?? string.Empty;
            _cell = cell;
            _holds = holds;
        }

        /// <summary>Works a formula out against the cells of a table.</summary>
        /// <param name="formula">The expression, without its <c>=</c>.</param>
        /// <param name="cell">The number a cell holds, by its row and column from nought; null where it holds none (empty, or words that are no number).</param>
        /// <param name="holds">Whether a cell holds anything at all (for Average and Count to leave the empty ones out).</param>
        /// <param name="value">The value.</param>
        /// <param name="isWhole">Whether the value is whole and every number that made it was.</param>
        /// <returns>False where the formula is not one, or reads a cell of words.</returns>
        internal static bool TryEvaluate(
            string formula,
            Func<int, int, double?> cell,
            Func<int, int, bool> holds,
            out double value,
            out bool isWhole
        )
        {
            var reader = new CadTableFormula(formula, cell, holds);

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

        /// <summary>The row and column, from nought, a reference such as C3 names; false for any other text.</summary>
        /// <param name="text">The reference.</param>
        /// <param name="row">The row.</param>
        /// <param name="column">The column.</param>
        /// <returns>Whether it is a reference.</returns>
        internal static bool TryReference(string text, out int row, out int column)
        {
            row = column = 0;
            Match match = Reference.Match(text ?? string.Empty);

            if (!match.Success)
            {
                return false;
            }

            column = ColumnOf(match.Groups[2].Value);
            row = int.Parse(match.Groups[4].Value, CultureInfo.InvariantCulture) - 1;
            return row >= 0;
        }

        /// <summary>The formula with its references moved, as a cell copied by Paste moves them: each reference not held by a <c>$</c> by the rows and columns given.</summary>
        /// <param name="formula">The formula, from its <c>=</c> or not.</param>
        /// <param name="rows">The rows to move down (up where below nought).</param>
        /// <param name="columns">The columns to move right.</param>
        /// <returns>The formula; a reference moved out of the table (row or column below the first) is left as it was.</returns>
        internal static string Shift(string formula, int rows, int columns) => Anywhere.Replace(formula, m =>
            {
                int column = ColumnOf(m.Groups[2].Value);
                int row = int.Parse(m.Groups[4].Value, CultureInfo.InvariantCulture) - 1;
                int newColumn = m.Groups[1].Length > 0 ? column : column + columns;
                int newRow = m.Groups[3].Length > 0 ? row : row + rows;

                if (newColumn < 0 || newRow < 0)
                {
                    return m.Value;
                }

                return m.Groups[1].Value
                    + CadTableEditor.ColumnName(newColumn)
                    + m.Groups[3].Value
                    + (newRow + 1).ToString(CultureInfo.InvariantCulture);
            });

        /// <summary>The cells a formula reads, each as the row and column of a reference or the corners of a range, so that the formulas that depend on a cell can be worked out after it.</summary>
        /// <param name="formula">The expression.</param>
        /// <returns>The references, a pair for each, in the order they stand.</returns>
        internal static List<int[]> Reads(string formula)
        {
            var found = new List<int[]>();

            foreach (Match m in Anywhere.Matches(formula ?? string.Empty))
            {
                found.Add(
                    new[]
                    {
                        int.Parse(m.Groups[4].Value, CultureInfo.InvariantCulture) - 1,
                        ColumnOf(m.Groups[2].Value),
                    }
                );
            }

            return found;
        }

        private static int ColumnOf(string letters)
        {
            int column = 0;

            foreach (char letter in letters.ToUpperInvariant())
            {
                column = column * 26 + (letter - 'A' + 1);
            }

            return column - 1;
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

            if (_at < _text.Length && (char.IsLetter(_text[_at]) || _text[_at] == '$'))
            {
                while (_at < _text.Length && (char.IsLetterOrDigit(_text[_at]) || _text[_at] == '$'))
                {
                    _at++;
                }

                string word = _text.Substring(start, _at - start);
                string name = word.ToLowerInvariant();
                int save = _at;

                if (!Take('('))
                {
                    _at = save;

                    if (name == "pi")
                    {
                        _real = true;
                        return Math.PI;
                    }

                    if (TryReference(word, out int row, out int column))
                    {
                        double? number = _cell(row, column);

                        if (!number.HasValue)
                        {
                            if (_holds(row, column))
                            {
                                throw new FormatException("A formula reads words that are no number.");
                            }

                            return 0.0;
                        }

                        if (Math.Abs(number.Value - Math.Round(number.Value)) > 1E-9)
                        {
                            _real = true;
                        }

                        return number.Value;
                    }

                    throw new FormatException("A formula names " + word + ", which is not a cell or a function.");
                }

                if (name == "sum" || name == "average" || name == "count")
                {
                    return Aggregate(name);
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
                    out double number2
                )
            )
            {
                throw new FormatException("A formula expects a number.");
            }

            if (_text.IndexOf('.', start, _at - start) >= 0)
            {
                _real = true;
            }

            return number2;
        }

        /// <summary>Sum, Average or Count over the ranges and cells between the brackets just opened.</summary>
        private double Aggregate(string name)
        {
            double sum = 0.0;
            int count = 0;
            bool any = false;

            do
            {
                Skip();
                int start = _at;

                while (_at < _text.Length && (char.IsLetterOrDigit(_text[_at]) || _text[_at] == '$'))
                {
                    _at++;
                }

                if (!TryReference(_text.Substring(start, _at - start), out int row, out int column))
                {
                    throw new FormatException("A formula names a cell or a range of cells here.");
                }

                int lastRow = row;
                int lastColumn = column;

                if (Take(':'))
                {
                    Skip();
                    int second = _at;

                    while (_at < _text.Length && (char.IsLetterOrDigit(_text[_at]) || _text[_at] == '$'))
                    {
                        _at++;
                    }

                    if (!TryReference(_text.Substring(second, _at - second), out lastRow, out lastColumn))
                    {
                        throw new FormatException("A range of cells ends at a cell.");
                    }
                }

                for (int r = Math.Min(row, lastRow); r <= Math.Max(row, lastRow); r++)
                {
                    for (int c = Math.Min(column, lastColumn); c <= Math.Max(column, lastColumn); c++)
                    {
                        double? number = _cell(r, c);

                        if (number.HasValue)
                        {
                            sum += number.Value;
                            count++;
                            any = true;
                        }
                        else if (_holds(r, c) && name != "count")
                        {
                            throw new FormatException("A formula reads words that are no number.");
                        }
                    }
                }
            } while (Take(','));

            if (!Take(')'))
            {
                throw new FormatException("A bracket of a formula is not closed.");
            }

            switch (name)
            {
                case "count":
                    return count;
                case "average":
                    if (!any)
                    {
                        throw new FormatException("A formula takes the average of no number.");
                    }

                    double mean = sum / count;

                    if (Math.Abs(mean - Math.Round(mean)) > 1E-9)
                    {
                        _real = true;
                    }

                    return mean;
                default:
                    return sum;
            }
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

        /// <summary>The words a formula that cannot be worked out shows in its cell, as AutoCAD shows an error.</summary>
        internal const string Error = "#####";
    }
}
