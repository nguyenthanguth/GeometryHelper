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

                foreach (Action define in _comLoaders)
                {
                    define();
                }

                _comLoaders.Clear();
            }

            return _empty;
        }
    }
}
