using System;
using System.Collections.Generic;

namespace GeometryHelper.PyStubs
{
    /// <summary>
    /// What the stubs need to know about Python's own words: the hard keywords a .NET name cannot use, the names a
    /// type of ours must not take because the stubs already use them, and the special methods that .NET operators become.
    /// </summary>
    internal static class PythonNames
    {
        private static readonly HashSet<string> Keywords = new HashSet<string>(StringComparer.Ordinal)
        {
            "False",
            "None",
            "True",
            "and",
            "as",
            "assert",
            "async",
            "await",
            "break",
            "class",
            "continue",
            "def",
            "del",
            "elif",
            "else",
            "except",
            "finally",
            "for",
            "from",
            "global",
            "if",
            "import",
            "in",
            "is",
            "lambda",
            "nonlocal",
            "not",
            "or",
            "pass",
            "raise",
            "return",
            "try",
            "while",
            "with",
            "yield",
        };

        /// <summary>Names the stub text uses itself; a type of ours with one of them would hide it.</summary>
        private static readonly HashSet<string> ReservedTypeNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "Any",
            "Callable",
            "ClassVar",
            "Iterator",
            "Sequence",
            "System",
            "bool",
            "float",
            "int",
            "object",
            "overload",
            "property",
            "staticmethod",
            "str",
            "tuple",
        };

        /// <summary>The special method of each binary operator that Python has a form for.</summary>
        private static readonly Dictionary<string, string> BinaryOperators = new Dictionary<string, string>(
            StringComparer.Ordinal
        )
        {
            ["op_Addition"] = "add",
            ["op_Division"] = "truediv",
            ["op_Multiply"] = "mul",
            ["op_Subtraction"] = "sub",
        };

        private static readonly Dictionary<string, string> Comparisons = new Dictionary<string, string>(
            StringComparer.Ordinal
        )
        {
            ["op_GreaterThan"] = "__gt__",
            ["op_GreaterThanOrEqual"] = "__ge__",
            ["op_LessThan"] = "__lt__",
            ["op_LessThanOrEqual"] = "__le__",
        };

        /// <summary>Whether <paramref name="name"/> is a Python hard keyword, which cannot be a name in a stub.</summary>
        public static bool IsKeyword(string name) => Keywords.Contains(name);

        /// <summary>Whether a type of ours may not be given this name.</summary>
        public static bool IsReservedTypeName(string name) => ReservedTypeNames.Contains(name);

        /// <summary>
        /// The Python name of a parameter: the .NET one, with an underscore after it when it is a keyword
        /// (<c>from</c> becomes <c>from_</c>).
        /// </summary>
        public static string ParameterName(string name) => IsKeyword(name) ? name + "_" : name;

        /// <summary>
        /// The special method of a binary operator: <c>__add__</c> when the left operand is the class itself,
        /// <c>__radd__</c> when it is the right operand (<paramref name="reflected"/>), or null when Python has no form for it.
        /// Comparisons have no reflected form here and give null for it.
        /// </summary>
        public static string BinaryOperator(string netName, bool reflected)
        {
            if (BinaryOperators.TryGetValue(netName, out string name))
            {
                return reflected ? "__r" + name + "__" : "__" + name + "__";
            }

            return !reflected && Comparisons.TryGetValue(netName, out string comparison) ? comparison : null;
        }

        /// <summary>The special method of a unary operator, or null when Python has no form for it.</summary>
        public static string UnaryOperator(string netName) => netName == "op_UnaryNegation" ? "__neg__" : null;
    }
}
