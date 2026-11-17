using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using GeometryHelper.Dxf;

namespace GeometryHelper.Cad
{
    /// <summary>One test of a property filter, as AutoCAD's <c>RelationalExpression</c>: a layer property and the pattern its text has to match.</summary>
    public sealed class CadLayerRule
    {
        /// <summary>Makes a rule.</summary>
        /// <param name="variable">The property: NAME, COLOR, LINETYPE, LINEWEIGHT, TRANSPARENCY, PLOTSTYLE, PLOTTABLE, NEWVPFROZEN, DESCRIPTION, OFF, FROZEN, LOCKED, VPFROZEN, USED, UNRECONCILED or VPOVERRIDES.</param>
        /// <param name="pattern">The pattern, with the wild cards of AutoCAD: <c>*</c> any run of letters, <c>?</c> one letter, <c>[AB]</c> one of those, a leading <c>~</c> anything the rest does not match.</param>
        /// <param name="negated">Whether the layer has to fail the pattern (<c>!=</c>).</param>
        public CadLayerRule(string variable, string pattern, bool negated = false)
        {
            Variable = (variable ?? string.Empty).Trim().ToUpperInvariant();
            Pattern = pattern ?? string.Empty;
            Negated = negated;
        }

        /// <summary>Gets the property, upper case.</summary>
        public string Variable { get; }

        /// <summary>Gets the pattern.</summary>
        public string Pattern { get; }

        /// <summary>Gets whether the layer has to fail the pattern.</summary>
        public bool Negated { get; }

        /// <summary>Gives the rule as AutoCAD writes it: <c>NAME=="A*"</c>.</summary>
        /// <returns>The text.</returns>
        public override string ToString() => Variable + (Negated ? "!=" : "==") + "\"" + Pattern + "\"";
    }

    /// <summary>
    /// The expression of a property filter, as AutoCAD's <c>FilterExpression</c> keeps it (group 301 of the filter's XRECORD):
    /// rules of the form <c>NAME=="A*"</c> joined by AND and OR, AND binding tighter, with brackets; read as a list of
    /// alternatives (OR), each a list of rules all of which a layer has to pass (AND). <c>NAME=="1_Arch*" AND OFF=="False"</c> is one
    /// alternative of two rules; <c>NAME=="*dim" OR NAME=="*text"</c> is two of one.
    /// </summary>
    public sealed class CadLayerExpression
    {
        // The properties the test of a layer reads; others pass no layer, as AutoCAD answers them.
        private static readonly string[] Known =
        {
            "NAME",
            "COLOR",
            "LINETYPE",
            "LINEWEIGHT",
            "TRANSPARENCY",
            "PLOTSTYLE",
            "PLOTTABLE",
            "NEWVPFROZEN",
            "DESCRIPTION",
            "OFF",
            "FROZEN",
            "LOCKED",
            "VPFROZEN",
            "USED",
            "UNRECONCILED",
            "VPOVERRIDES",
        };

        /// <summary>Makes an expression of alternatives.</summary>
        /// <param name="alternatives">The alternatives, each its rules.</param>
        public CadLayerExpression(IReadOnlyList<IReadOnlyList<CadLayerRule>> alternatives)
        {
            Alternatives = alternatives ?? throw new ArgumentNullException(nameof(alternatives));
        }

        /// <summary>Gets the alternatives: a layer passes when it passes all the rules of one of them; none passes every layer.</summary>
        public IReadOnlyList<IReadOnlyList<CadLayerRule>> Alternatives { get; }

        /// <summary>Gets the properties a filter can test, in the order of AutoCAD's columns.</summary>
        public static IReadOnlyList<string> Variables => Known;

        /// <summary>
        /// Reads an expression: <c>NAME=="A*" AND COLOR=="1"</c>, <c>OFF == "FALSE" and FROZEN == "FALSE"</c>, <c>( NAME== "A*" )</c>,
        /// letters of either case, spaces round the signs or none.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="expression">The expression; null where the text is not one.</param>
        /// <returns>Whether the text is an expression. An empty text is one, with no alternative.</returns>
        public static bool TryParse(string text, out CadLayerExpression expression)
        {
            expression = null;

            if (text == null)
            {
                return false;
            }

            var reader = new Reader(text);
            List<List<CadLayerRule>> alternatives = reader.Alternatives();

            if (alternatives == null || !reader.AtEnd)
            {
                return false;
            }

            var list = new List<IReadOnlyList<CadLayerRule>>();

            foreach (List<CadLayerRule> alternative in alternatives)
            {
                list.Add(alternative.AsReadOnly());
            }

            expression = new CadLayerExpression(list.AsReadOnly());
            return true;
        }

        /// <summary>Gives the expression as AutoCAD's filter dialog writes it: rules joined by AND, alternatives by OR, no brackets.</summary>
        /// <returns>The text; empty for no alternative.</returns>
        public override string ToString()
        {
            var text = new StringBuilder();

            foreach (IReadOnlyList<CadLayerRule> alternative in Alternatives)
            {
                if (alternative.Count == 0)
                {
                    continue;
                }

                if (text.Length > 0)
                {
                    text.Append(" OR ");
                }

                for (int i = 0; i < alternative.Count; i++)
                {
                    text.Append(i == 0 ? string.Empty : " AND ").Append(alternative[i]);
                }
            }

            return text.ToString();
        }

        /// <summary>
        /// Whether a layer passes: one alternative whose rules it passes all. Where the expression has no rule every layer passes; a
        /// rule of a property the library does not know passes none, as AutoCAD answers one.
        /// </summary>
        /// <param name="layer">The layer.</param>
        /// <param name="context">What the layer's properties are read against.</param>
        /// <returns>True when it passes.</returns>
        public bool Matches(DxfLayerTableRecord layer, CadLayerFilterContext context)
        {
            bool any = false;

            foreach (IReadOnlyList<CadLayerRule> alternative in Alternatives)
            {
                if (alternative.Count == 0)
                {
                    continue;
                }

                any = true;
                bool all = true;

                foreach (CadLayerRule rule in alternative)
                {
                    if (!Passes(rule, layer, context))
                    {
                        all = false;
                        break;
                    }
                }

                if (all)
                {
                    return true;
                }
            }

            return !any;
        }

        /// <summary>The text of a layer's property a rule is matched against; null for a property that is not known.</summary>
        internal static string ValueOf(string variable, DxfLayerTableRecord layer, CadLayerFilterContext context)
        {
            switch (variable)
            {
                case "NAME":
                    return layer.Name;
                case "COLOR":
                    return layer.TrueColor.HasValue
                        ? ((layer.TrueColor.Value >> 16) & 255).ToString(CultureInfo.InvariantCulture)
                            + ","
                            + ((layer.TrueColor.Value >> 8) & 255).ToString(CultureInfo.InvariantCulture)
                            + ","
                            + (layer.TrueColor.Value & 255).ToString(CultureInfo.InvariantCulture)
                        : Math.Abs(layer.ColorIndex).ToString(CultureInfo.InvariantCulture);
                case "LINETYPE":
                    return layer.LinetypeName;
                case "LINEWEIGHT":
                    return layer.LineWeight == DxfLineWeight.ByLineWeightDefault
                        ? "Default"
                        : ((int)layer.LineWeight / 100.0).ToString("0.00", CultureInfo.InvariantCulture);
                case "TRANSPARENCY":
                    return CadLayerEditor
                        .TransparencyPercent(layer.Transparency)
                        .ToString(CultureInfo.InvariantCulture);
                case "PLOTSTYLE":
                    return context.PlotStyleOf(layer);
                case "PLOTTABLE":
                    return Bool(layer.IsPlottable);
                case "NEWVPFROZEN":
                    return Bool(layer.ViewportVisibilityDefault);
                case "DESCRIPTION":
                    return layer.Description;
                case "OFF":
                    return Bool(layer.IsOff);
                case "FROZEN":
                    return Bool(layer.IsFrozen);
                case "LOCKED":
                    return Bool(layer.IsLocked);
                case "VPFROZEN":
                    return Bool(context.Viewport != null && context.Viewport.IsLayerFrozenInViewport(layer));
                case "USED":
                    return Bool(context.IsUsed(layer));
                case "UNRECONCILED":
                    return Bool(false);
                case "VPOVERRIDES":
                    return Bool(layer.HasOverrides);
                default:
                    return null;
            }
        }

        private static string Bool(bool value) => value ? "True" : "False";

        private static bool Passes(CadLayerRule rule, DxfLayerTableRecord layer, CadLayerFilterContext context)
        {
            string value = ValueOf(rule.Variable, layer, context);

            if (value == null)
            {
                return false;
            }

            // An empty pattern asks nothing, as AutoCAD answers DESCRIPTION=="".
            bool matches = rule.Pattern.Length == 0 || CadWildcard.IsMatch(value, EscapeCommas(rule.Pattern), true);
            return rule.Negated ? !matches : matches;
        }

        // A comma between patterns is "or" in wcmatch; AutoCAD's filter matches the comma as it is.
        private static string EscapeCommas(string pattern) => pattern.Replace(",", "`,");

        /// <summary>A recursive-descent reader of the text: or-of-and, brackets, rules.</summary>
        private sealed class Reader
        {
            private readonly string _text;
            private int _at;

            internal Reader(string text)
            {
                _text = text;
            }

            internal bool AtEnd
            {
                get
                {
                    Skip();
                    return _at >= _text.Length;
                }
            }

            internal List<List<CadLayerRule>> Alternatives()
            {
                Skip();

                if (_at >= _text.Length)
                {
                    return new List<List<CadLayerRule>>();
                }

                List<List<CadLayerRule>> result = Conjunction();

                while (result != null && Word("OR"))
                {
                    List<List<CadLayerRule>> more = Conjunction();

                    if (more == null)
                    {
                        return null;
                    }

                    result.AddRange(more);
                }

                return result;
            }

            private List<List<CadLayerRule>> Conjunction()
            {
                List<List<CadLayerRule>> result = Primary();

                while (result != null && Word("AND"))
                {
                    List<List<CadLayerRule>> next = Primary();

                    if (next == null)
                    {
                        return null;
                    }

                    // Distributed: (a OR b) AND c is a AND c, b AND c.
                    var product = new List<List<CadLayerRule>>();

                    foreach (List<CadLayerRule> left in result)
                    {
                        foreach (List<CadLayerRule> right in next)
                        {
                            var joined = new List<CadLayerRule>(left);
                            joined.AddRange(right);
                            product.Add(joined);
                        }
                    }

                    result = product;
                }

                return result;
            }

            private List<List<CadLayerRule>> Primary()
            {
                Skip();

                if (_at < _text.Length && _text[_at] == '(')
                {
                    _at++;
                    List<List<CadLayerRule>> inner = Alternatives();
                    Skip();

                    if (inner == null || inner.Count == 0 || _at >= _text.Length || _text[_at] != ')')
                    {
                        return null;
                    }

                    _at++;
                    return inner;
                }

                int start = _at;

                while (_at < _text.Length && (char.IsLetterOrDigit(_text[_at]) || _text[_at] == '_'))
                {
                    _at++;
                }

                if (_at == start)
                {
                    return null;
                }

                string variable = _text.Substring(start, _at - start);
                Skip();
                bool negated;

                if (string.CompareOrdinal(_text, _at, "==", 0, 2) == 0)
                {
                    negated = false;
                }
                else if (string.CompareOrdinal(_text, _at, "!=", 0, 2) == 0)
                {
                    negated = true;
                }
                else
                {
                    return null;
                }

                _at += 2;
                Skip();

                if (_at >= _text.Length || _text[_at] != '"')
                {
                    return null;
                }

                int close = _text.IndexOf('"', _at + 1);

                if (close < 0)
                {
                    return null;
                }

                string pattern = _text.Substring(_at + 1, close - _at - 1);
                _at = close + 1;
                return new List<List<CadLayerRule>>
                {
                    new List<CadLayerRule> { new CadLayerRule(variable, pattern, negated) },
                };
            }

            /// <summary>Takes the word AND or OR, letters of either case, where it stands whole.</summary>
            private bool Word(string word)
            {
                Skip();

                if (
                    string.Compare(_text, _at, word, 0, word.Length, StringComparison.OrdinalIgnoreCase) != 0
                    || _at + word.Length > _text.Length
                )
                {
                    return false;
                }

                int end = _at + word.Length;

                if (end < _text.Length && char.IsLetterOrDigit(_text[end]))
                {
                    return false;
                }

                _at = end;
                return true;
            }

            private void Skip()
            {
                while (_at < _text.Length && char.IsWhiteSpace(_text[_at]))
                {
                    _at++;
                }
            }
        }
    }
}
