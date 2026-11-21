using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace GeometryHelper.Cad.Lisp
{
    public sealed partial class LispInterpreter
    {
        private readonly List<string> _exported = new List<string>();

        /// <summary>The number <see cref="LispApplication"/> gave this interpreter, to tell its own propagations from the others'.</summary>
        internal long ApplicationId;

        /// <summary>The last propagation this interpreter has looked at.</summary>
        internal long AppliedSequence;

        private void RegisterNamespaces()
        {
            Define("VL-BB-SET", 2, 2, a =>
            {
                LispApplication.SetBlackboard(NameOfSymbol(a[0]), a[1]);
                return a[1];
            });
            Define("VL-BB-REF", 1, 1, a => CopyInto(LispApplication.GetBlackboard(NameOfSymbol(a[0]))));
            Define("VL-PROPAGATE", 1, 1, Propagate);
            Define("VL-LOAD-ALL", 1, 1, LoadAll);
            Define("VL-DOC-REF", 1, 1, a =>
                _symbols.TryGetValue(NameOfSymbol(a[0]), out LispSymbol s) ? s.Value : null
            );
            Define("VL-DOC-SET", 2, 2, a => Intern(NameOfSymbol(a[0])).Value = a[1]);

            Define("VL-DOC-EXPORT", 1, 1, a =>
            {
                string name = NameOfSymbol(a[0]);

                if (!_exported.Contains(name))
                {
                    _exported.Add(name);
                }

                return _true;
            });
            Define("VL-LIST-EXPORTED-FUNCTIONS", 0, 1, a =>
            {
                if (a.Length > 0)
                {
                    throw ApplicationNotFound(a[0]);
                }

                return ListOf(_exported.ConvertAll(n => (object)n));
            });
            Define("VL-DOC-IMPORT", 1, -1, a => throw ApplicationNotFound(a[0]));
            Define("VL-UNLOAD-VLX", 1, 1, a => throw ApplicationNotFound(a[0]));
            Define("VL-VLX-LOADED-P", 1, 1, a => null);
            Define("VL-LIST-LOADED-VLX", 0, 0, a => null);
            Define("VL-GET-RESOURCE", 1, 2, a => null);

            Refuse("VL-VBALOAD", "VBA");
            Refuse("VL-VBARUN", "VBA");
            Refuse("VL-ARX-IMPORT", "ObjectARX");
            Refuse("VL-ACAD-DEFUN", "ObjectARX");
            Refuse("VL-ACAD-UNDEFUN", "ObjectARX");
        }

        /// <summary>
        /// Defines a function of AutoCAD alone, which the presenter does not have: calling it is an error naming it and what it
        /// would need, <c>vl-vbarun: VBA is not supported by GeometryHelper.Cad</c>, never a silent nil.
        /// </summary>
        private void Refuse(string name, string needs) =>
            Define(name, 0, -1, a =>
                throw new LispException(
                    name.ToLowerInvariant() + ": " + needs + " is not supported by GeometryHelper.Cad"
                )
            );

        /// <summary>Takes the name of a symbol an argument gives: a symbol, or a string for the symbol of that name.</summary>
        private static string NameOfSymbol(object value)
        {
            if (value is LispSymbol symbol)
            {
                return symbol.Name.ToUpperInvariant();
            }

            return value is string text ? text.ToUpperInvariant() : throw TypeError("symbolp", value);
        }

        /// <summary>The error a VLX function gives for an application that is not loaded: no VLX is, in the presenter.</summary>
        private static LispException ApplicationNotFound(object name) =>
            new LispException("LISP Application is not found " + LispPrinter.Print(name, false));

        /// <summary>
        /// <c>(vl-propagate symbol)</c>: the symbol's value goes to every other interpreter of the process, at its next
        /// evaluation, and is what the function gives; a symbol with no value gives nil and goes nowhere.
        /// </summary>
        private object Propagate(object[] a)
        {
            LispSymbol symbol = a[0] as LispSymbol ?? throw TypeError("symbolp", a[0]);

            if (symbol.Value == null)
            {
                return null;
            }

            LispApplication.Propagate(this, symbol.Name, symbol.Value, IsDefined(symbol));
            return symbol.Value;
        }

        /// <summary><c>(vl-load-all file)</c>: loads the file as <c>load</c> does, and in every other interpreter at its next evaluation.</summary>
        private object LoadAll(object[] a)
        {
            string name = Str(a[0]);
            object last = LoadFile(name, false, null, out string path);

            if (path != null)
            {
                LispApplication.PropagateLoad(this, path);
            }

            return last;
        }

        /// <summary>Takes a symbol and its value from another interpreter, the value copied.</summary>
        /// <param name="name">The name, in upper case.</param>
        /// <param name="value">The value in the other interpreter.</param>
        /// <param name="defined">True when it is a function made by <c>defun</c>.</param>
        internal void ApplyPropagated(string name, object value, bool defined)
        {
            object copy;

            try
            {
                copy = CopyInto(value);
            }
            catch (LispException)
            {
                // A value nested past the stack limit is not taken.
                return;
            }

            LispSymbol symbol = Intern(name);
            symbol.Value = copy;

            if (defined && copy != null)
            {
                _defined[symbol] = copy;
            }
        }

        /// <summary>Loads a file another interpreter loaded; an error in it is not reported, since nobody is waiting for it.</summary>
        /// <param name="path">The full path of the file.</param>
        internal void ApplyLoaded(string path)
        {
            try
            {
                LoadFile(path, false, null);
            }
            catch (LispException error)
            {
                Restore(error.Frames);
            }
            catch (Exception exception) when (!(exception is ThreadAbortException)) { }
        }

        /// <summary>
        /// Makes a copy of a value as this interpreter would hold it, for a value that crosses from another: symbols are
        /// interned again by name, lists copied through, a built-in function is this interpreter's own of that name (nil when it
        /// has none), a function written in AutoLISP is rebuilt from its arguments and body; strings, numbers, entity names and
        /// selection sets stay as they are.
        /// </summary>
        /// <param name="value">The value, as another interpreter held it.</param>
        /// <returns>The copy.</returns>
        internal object CopyInto(object value)
        {
            switch (value)
            {
                case LispSymbol symbol:
                    if (symbol.Name.Length == 0)
                    {
                        return _empty;
                    }

                    // The one symbol that is not upper case is the lower-case quote of a function value's wrapper.
                    return symbol.Name == "quote" ? _quoteLower : Intern(symbol.Name.ToUpperInvariant());

                case LispCons cons:
                    CheckStack();
                    var items = new List<object>();
                    object cell = cons;

                    while (cell is LispCons c)
                    {
                        items.Add(CopyInto(c.Car));
                        cell = c.Cdr;
                    }

                    object list = CopyInto(cell);

                    for (int i = items.Count - 1; i >= 0; i--)
                    {
                        list = new LispCons(items[i], list);
                    }

                    return list;

                case LispLambda lambda:
                    CheckStack();
                    var parameters = new LispSymbol[lambda.Parameters.Length];
                    var locals = new LispSymbol[lambda.Bound.Length - parameters.Length];

                    for (int i = 0; i < lambda.Bound.Length; i++)
                    {
                        LispSymbol made = Intern(lambda.Bound[i].Name);

                        if (i < parameters.Length)
                        {
                            parameters[i] = made;
                        }
                        else
                        {
                            locals[i - parameters.Length] = made;
                        }
                    }

                    return new LispLambda(lambda.Name, parameters, locals, CopyInto(lambda.Body));

                case LispBuiltin builtin:
                    return Intern(builtin.Name).Value as LispBuiltin;

                default:
                    return value;
            }
        }
    }
}
