using System;
using System.Collections.Generic;

namespace GeometryHelper.Cad.Lisp
{
    /// <summary>
    /// <c>vl-load-com</c> and what it brings: AutoCAD 2021 has only <c>vlax-for</c> before it, and <c>:vlax-true</c> and
    /// <c>:vlax-false</c> evaluate to themselves; the first <c>(vl-load-com)</c> defines the <c>vla-</c> and <c>vlax-</c>
    /// functions. The families register what they define with <see cref="OnLoadCom"/>, and it is defined once per interpreter.
    /// </summary>
    public sealed partial class LispInterpreter
    {
        private readonly List<Action> _comLoaders = new List<Action>();

        /// <summary>Gets whether <c>(vl-load-com)</c> has run in this interpreter.</summary>
        internal bool IsComLoaded { get; private set; }

        /// <summary>Defines <c>:vlax-true</c>, <c>:vlax-false</c> and <c>vl-load-com</c>; called by the constructor.</summary>
        private void RegisterCom()
        {
            LispSymbol yes = InternPrinted(":vlax-true");
            yes.Value = yes;
            LispSymbol no = InternPrinted(":vlax-false");
            no.Value = no;
            InternPrinted("variant");
            InternPrinted("safearray");
            Define("VL-LOAD-COM", 0, 0, a => LoadCom());
            RegisterVariants();
        }

        /// <summary>Registers the variant and safearray functions with <see cref="OnLoadCom"/>; implemented where they are.</summary>
        partial void RegisterVariants();

        /// <summary>
        /// Has a family's functions defined at the first <c>(vl-load-com)</c>, or at once where it has run already.
        /// </summary>
        /// <param name="define">What defines the functions.</param>
        internal void OnLoadCom(Action define)
        {
            if (IsComLoaded)
            {
                define();
            }
            else
            {
                _comLoaders.Add(define);
            }
        }

        /// <summary>
        /// Gets the symbol of a name, printed as given: AutoCAD prints the types <c>variant</c> and <c>safearray</c> in lower
        /// case, while the symbol is still the one <c>'variant</c> reads. A symbol that exists already keeps its print name.
        /// </summary>
        /// <param name="printName">The name as it prints.</param>
        /// <returns>The symbol.</returns>
        internal LispSymbol InternPrinted(string printName)
        {
            string name = printName.ToUpperInvariant();

            if (!_symbols.TryGetValue(name, out LispSymbol symbol))
            {
                symbol = new LispSymbol(printName);
                _symbols.Add(name, symbol);
            }

            return symbol;
        }

        /// <summary>The body of <c>vl-load-com</c>: defines what the families registered, once; gives the empty symbol.</summary>
        private object LoadCom()
        {
            if (!IsComLoaded)
            {
                IsComLoaded = true;
                var before = new HashSet<LispSymbol>();

                foreach (LispSymbol symbol in _symbols.Values)
                {
                    if (symbol.Value is LispBuiltin)
                    {
                        before.Add(symbol);
                    }
                }

                foreach (Action define in _comLoaders)
                {
                    define();
                }

                _comLoaders.Clear();
                RegisterCase(before);
                RegisterConstants();
            }

            return _empty;
        }

        // The ActiveX functions whose registered case is not all lower case (AutoCAD 2021, probe f2consts: vlax-curve-getEndParam,
        // vlax-map-Collection, vlax-vbDouble ...), and the two old names of variant-value and safearray-value, which are lower case
        // too. Every other function this load defines whose name starts with vlax- is registered all lower case (vlax-invoke),
        // and the vla- ones are registered as the library spells them (vla-get-Name) by their own definition. vlax-for and textbox
        // exist before the load and stay as they are (VLAX-FOR, TEXTBOX).
        private static readonly string[] RegisteredCase =
        {
            "vlax-curve-getArea",
            "vlax-curve-getClosestPointTo",
            "vlax-curve-getClosestPointToProjection",
            "vlax-curve-getDistAtParam",
            "vlax-curve-getDistAtPoint",
            "vlax-curve-getEndParam",
            "vlax-curve-getEndPoint",
            "vlax-curve-getFirstDeriv",
            "vlax-curve-getParamAtDist",
            "vlax-curve-getParamAtPoint",
            "vlax-curve-getPointAtDist",
            "vlax-curve-getPointAtParam",
            "vlax-curve-getSecondDeriv",
            "vlax-curve-getStartParam",
            "vlax-curve-getStartPoint",
            "vlax-curve-isClosed",
            "vlax-curve-isPeriodic",
            "vlax-curve-isPlanar",
            "vlax-map-Collection",
            "vlax-vbArray",
            "vlax-vbBoolean",
            "vlax-vbDouble",
            "vlax-vbEmpty",
            "vlax-vbInteger",
            "vlax-vbLong",
            "vlax-vbNull",
            "vlax-vbObject",
            "vlax-vbSingle",
            "vlax-vbString",
            "vlax-vbVariant",
            "variant-value",
            "safearray-value",
        };

        /// <summary>
        /// Gives the ActiveX functions this load defined the case they print in as AutoCAD registers them (probe f2consts of the L4
        /// acceptance: <c>(vl-prin1-to-string '(vla-get-Name vlax-get-acad-object vlax-for foo))</c> is
        /// <c>(vla-get-Name vlax-get-acad-object VLAX-FOR FOO)</c>), also for a symbol a routine read before the load.
        /// </summary>
        /// <param name="before">The symbols that held a built-in before the load: they keep their names.</param>
        private void RegisterCase(HashSet<LispSymbol> before)
        {
            foreach (LispSymbol symbol in _symbols.Values)
            {
                if (
                    symbol.Value is LispBuiltin
                    && !before.Contains(symbol)
                    && symbol.Name.StartsWith("VLAX-", StringComparison.Ordinal)
                )
                {
                    symbol.Name = symbol.Name.ToLowerInvariant();
                }
            }

            foreach (string registered in RegisteredCase)
            {
                Registered(registered);
            }
        }

        /// <summary>
        /// Binds the constants of the enumerations of the ActiveX library (<c>acSelectionSetAll</c>, <c>acTrue</c>, <c>acRed</c> ...: 898
        /// names, AutoCAD 2021, probe f2consts) to their values, in the case the library spells them; a symbol a routine gave a value
        /// to before the load keeps it.
        /// </summary>
        private void RegisterConstants()
        {
            foreach (KeyValuePair<string, int> constant in LispActiveXConstants.All())
            {
                LispSymbol symbol = Registered(constant.Key);

                if (symbol.Value == null)
                {
                    symbol.Value = constant.Value;
                }
            }
        }

        /// <summary>The symbol of a name, which from now on prints as given (<c>vla-get-Name</c>, <c>acRed</c>), whatever case it was read in.</summary>
        /// <param name="printName">The name as the library registers it.</param>
        /// <returns>The symbol.</returns>
        internal LispSymbol Registered(string printName)
        {
            LispSymbol symbol = Intern(printName.ToUpperInvariant());
            symbol.Name = printName;
            return symbol;
        }
    }
}
