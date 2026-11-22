using System;
using System.Collections.Generic;

namespace GeometryHelper.Cad.Lisp
{
    /// <summary>
    /// The ActiveX functions of AutoLISP on the presenter's own object model (<see cref="LispActiveX"/>): <c>vlax-for</c>, which
    /// AutoCAD has before <c>vl-load-com</c> and so every session interpreter has, and, defined by the first
    /// <c>(vl-load-com)</c>, the object functions (<c>vlax-ename-&gt;vla-object</c>, <c>vlax-get-property</c>,
    /// <c>vlax-invoke-method</c>, the old <c>vlax-get</c>, <c>vlax-put</c> and <c>vlax-invoke</c>, <c>vlax-map-collection</c>,
    /// <c>vlax-release-object</c> ...), the <c>vla-get-</c>, <c>vla-put-</c> and <c>vla-</c> function of every member the
    /// object model has, and the refusals of COM to other programs (Excel). The members are those of
    /// <see cref="LispActiveX"/>'s registry; objects are read and written on the owner thread.
    /// </summary>
    internal static partial class LispDrawing
    {
        static partial void RegisterActiveX(LispInterpreter lisp, CadCommandSession session)
        {
            var ax = new LispActiveX(lisp, session, StateOf(lisp));
            lisp.DefineSpecial("VLAX-FOR", forms => VlaxFor(lisp, ax, forms));
            lisp.OnLoadCom(() => DefineActiveX(lisp, ax));
        }

        private static void DefineActiveX(LispInterpreter lisp, LispActiveX ax)
        {
            lisp.Define("VLAX-GET-ACAD-OBJECT", 0, 0, a => lisp.OnOwner(() => ax.Application));
            lisp.Define("VLAX-ENAME->VLA-OBJECT", 1, 1, a =>
            {
                LispEntityName name = NameOf(a[0]);
                return lisp.OnOwner(() => ax.OfName(name));
            });
            lisp.Define("VLAX-VLA-OBJECT->ENAME", 1, 1, a =>
            {
                LispVlaObject target = LispActiveX.ObjectOf(a[0]);
                return lisp.OnOwner(() => target.IsReleased || ax.IsErased(target) ? null : ax.NameOf(target));
            });

            lisp.Define("VLAX-GET-PROPERTY", 2, 2, a => GetProperty(lisp, ax, a, legacy: false));
            lisp.Define("VLAX-GET", 2, 2, a => GetProperty(lisp, ax, a, legacy: true));
            lisp.Define("VLAX-PUT-PROPERTY", 3, 3, a => PutProperty(lisp, ax, a, legacy: false));
            lisp.Define("VLAX-PUT", 3, 3, a => PutProperty(lisp, ax, a, legacy: true));
            lisp.Define("VLAX-INVOKE-METHOD", 2, -1, a => InvokeMethod(lisp, ax, a, legacy: false));
            lisp.Define("VLAX-INVOKE", 2, -1, a => InvokeMethod(lisp, ax, a, legacy: true));

            lisp.Define("VLAX-MAP-COLLECTION", 2, 2, a => MapCollection(lisp, ax, a));
            lisp.Define("VLAX-DUMP-OBJECT", 1, 2, a =>
            {
                LispVlaObject target = LispActiveX.ObjectOf(a[0]);
                bool methods = a.Length > 1 && a[1] != null;
                string text = (string)lisp.OnOwner(() => ax.DumpText(target, methods));
                lisp.WriteFreshLine();
                lisp.Write(text);
                return lisp.Bool(true);
            });
            lisp.Define("VLAX-PROPERTY-AVAILABLE-P", 2, 3, a =>
            {
                LispVlaObject target = LispActiveX.ObjectOf(a[0]);
                string name = LispActiveX.NameOfMember(a[1]);
                bool writable = a.Length > 2 && a[2] != null;
                return lisp.OnOwner(() =>
                {
                    ax.EnsureLive(target);
                    return lisp.Bool(ax.HasProperty(target, name, writable));
                });
            });
            lisp.Define("VLAX-METHOD-APPLICABLE-P", 2, 2, a =>
            {
                LispVlaObject target = LispActiveX.ObjectOf(a[0]);
                string name = LispActiveX.NameOfMember(a[1]);
                return lisp.OnOwner(() =>
                {
                    ax.EnsureLive(target);
                    return lisp.Bool(ax.HasMethod(target, name));
                });
            });
            lisp.Define("VLAX-TYPEINFO-AVAILABLE-P", 1, 1, a =>
            {
                LispActiveX.ObjectOf(a[0]);
                return lisp.Bool(true);
            });

            lisp.Define("VLAX-OBJECT-RELEASED-P", 1, 1, a => lisp.Bool(LispActiveX.ObjectOf(a[0]).IsReleased));
            lisp.Define("VLAX-RELEASE-OBJECT", 1, 1, a =>
            {
                LispVlaObject target = LispActiveX.ObjectOf(a[0]);

                if (target.IsReleased)
                {
                    throw LispActiveX.Released();
                }

                ax.Release(target);
                return 0;
            });
            lisp.Define("VLAX-ERASED-P", 1, 1, a =>
            {
                LispVlaObject target = ObjectWithId(a[0]);
                return lisp.OnOwner(() => lisp.Bool(ax.IsErased(target)));
            });
            lisp.Define("VLAX-READ-ENABLED-P", 1, 1, a =>
            {
                LispVlaObject target = ObjectWithId(a[0]);
                return lisp.OnOwner(() => lisp.Bool(!ax.IsErased(target)));
            });
            lisp.Define("VLAX-WRITE-ENABLED-P", 1, 1, a =>
            {
                LispVlaObject target = ObjectWithId(a[0]);
                return lisp.OnOwner(() => lisp.Bool(ax.IsWritable(target)));
            });

            DefineRefusals(lisp);
            DefineMemberFunctions(lisp, ax);
        }

        /// <summary>The object an argument has to be where AutoCAD asks for its ObjectID: <c>unable to get ObjectID: nil</c> for anything else, and for a released value (AutoCAD 2021, a2 and GUI g2).</summary>
        private static LispVlaObject ObjectWithId(object value)
        {
            if (value is LispVlaObject target && !target.IsReleased)
            {
                return target;
            }

            throw new LispException("unable to get ObjectID: " + LispInterpreter.Print(value, true));
        }

        #region vlax-for and vlax-map-collection

        /// <summary>
        /// <c>(vlax-for symbol collection expression...)</c>: the symbol is each member of the collection in <c>Item</c> order
        /// while the expressions are evaluated, and has its old value after. The collection is a collection, a block or a
        /// selection set; anything else is <c>bad argument type: VLA-object collection:</c>. Gives the value of the last
        /// expression of the last member, nil for an empty collection or no expression (AutoCAD 2021, feed fdval:
        /// <c>(vlax-for b (vla-get-Blocks doc) 5)</c> gives 5).
        /// </summary>
        private static object VlaxFor(LispInterpreter lisp, LispActiveX ax, object forms)
        {
            if (!(forms is LispCons c) || !(c.Car is LispSymbol variable) || !(c.Cdr is LispCons rest))
            {
                throw new LispException("syntax error");
            }

            object given = lisp.Eval(rest.Car);
            LispVlaObject target =
                given as LispVlaObject ?? throw LispInterpreter.TypeError("VLA-object collection:", given);
            var items = (List<LispVlaObject>)lisp.OnOwner(() => CollectionItems(ax, target));
            var symbols = new[] { variable };
            var saved = new[] { variable.Value };
            object value = null;

            try
            {
                foreach (LispVlaObject item in items)
                {
                    variable.Value = item;
                    value = null;

                    for (object cell = rest.Cdr; cell is LispCons body; cell = body.Cdr)
                    {
                        value = lisp.Eval(body.Car);
                    }
                }
            }
            catch (LispException error) when (error.Record(symbols, saved))
            {
                throw;
            }

            variable.Value = saved[0];
            return value;
        }

        private static List<LispVlaObject> CollectionItems(LispActiveX ax, LispVlaObject target)
        {
            ax.EnsureLive(target);
            return ax.ItemsOfCollection(target);
        }

        /// <summary><c>(vlax-map-collection collection function)</c>: calls the function with each member, and gives the collection (AutoCAD 2021, GUI g2); a value that is no collection is <c>not a collection:</c>.</summary>
        private static object MapCollection(LispInterpreter lisp, LispActiveX ax, object[] a)
        {
            LispVlaObject target = LispActiveX.ObjectOf(a[0]);

            if (
                target.Kind != LispVlaKind.Collection
                && target.Kind != LispVlaKind.Block
                && target.Kind != LispVlaKind.SelectionSet
            )
            {
                throw new LispException("not a collection: " + LispInterpreter.Print(target, true));
            }

            LispFunction function = lisp.Callable(a[1], catchAll: false);
            var items = (List<LispVlaObject>)lisp.OnOwner(() => CollectionItems(ax, target));

            foreach (LispVlaObject item in items)
            {
                lisp.Apply(function, new object[] { item });
            }

            return target;
        }

        #endregion

        #region vlax-get-property and the others

        private static object GetProperty(LispInterpreter lisp, LispActiveX ax, object[] a, bool legacy)
        {
            LispVlaObject target = LispActiveX.ObjectOf(a[0]);
            string name = LispActiveX.NameOfMember(a[1]);
            return lisp.OnOwner(() => ax.GetProperty(target, name, legacy, name));
        }

        private static object PutProperty(LispInterpreter lisp, LispActiveX ax, object[] a, bool legacy)
        {
            LispVlaObject target = LispActiveX.ObjectOf(a[0]);
            string name = LispActiveX.NameOfMember(a[1]);
            object value = a[2];
            lisp.OnOwner(() =>
            {
                ax.PutProperty(target, name, value, legacy, name);
                return null;
            });
            return null;
        }

        private static object InvokeMethod(LispInterpreter lisp, LispActiveX ax, object[] a, bool legacy)
        {
            LispVlaObject target = LispActiveX.ObjectOf(a[0]);
            string name = LispActiveX.NameOfMember(a[1]);
            var rest = new object[a.Length - 2];
            Array.Copy(a, 2, rest, 0, rest.Length);
            return lisp.OnOwner(() => ax.Invoke(target, name, rest, legacy, name));
        }

        #endregion

        #region The vla- functions

        /// <summary>
        /// Defines <c>vla-get-P</c> for each property, <c>vla-put-P</c> for each writable one and <c>vla-M</c> for each method of
        /// the registry (D213), named as the reference spells them. A count of arguments that does not fit is
        /// <c>Too many actual parameters</c> or <c>too few actual parameters</c> (AutoCAD 2021, GUI g2, g3).
        /// </summary>
        private static void DefineMemberFunctions(LispInterpreter lisp, LispActiveX ax)
        {
            foreach (string registered in ax.Names)
            {
                IReadOnlyList<LispAxMember> members = ax.MembersOf(registered);
                string name = members[0].Name;
                bool read = false;
                bool write = false;
                bool call = false;

                foreach (LispAxMember member in members)
                {
                    read |= member.Get != null;
                    write |= member.Get != null && member.Put != null;
                    call |= member.Call != null;
                }

                if (read)
                {
                    DefineNamed(lisp, "vla-get-" + name, a =>
                    {
                        LispVlaObject target = LispActiveX.ObjectOf(a[0]);

                        if (a.Length > 1)
                        {
                            throw new LispException("Too many actual parameters");
                        }

                        return lisp.OnOwner(() => ax.GetProperty(target, name, false, name));
                    });
                }

                if (write)
                {
                    DefineNamed(lisp, "vla-put-" + name, a =>
                    {
                        LispVlaObject target = LispActiveX.ObjectOf(a[0]);

                        if (a.Length < 2)
                        {
                            throw new LispException("too few actual parameters");
                        }

                        if (a.Length > 2)
                        {
                            throw new LispException("Too many actual parameters");
                        }

                        object value = a[1];
                        lisp.OnOwner(() =>
                        {
                            ax.PutProperty(target, name, value, false, name);
                            return null;
                        });
                        return null;
                    });
                }

                if (call)
                {
                    DefineNamed(lisp, "vla-" + name, a =>
                    {
                        LispVlaObject target = LispActiveX.ObjectOf(a[0]);
                        var rest = new object[a.Length - 1];
                        Array.Copy(a, 1, rest, 0, rest.Length);
                        return lisp.OnOwner(() => ax.Invoke(target, name, rest, false, name));
                    });
                }
            }
        }

        /// <summary>Defines a function under a name that prints as given (<c>vla-get-Name</c>, as in AutoCAD 2021) while the symbol is the one the reader finds in any case.</summary>
        private static void DefineNamed(LispInterpreter lisp, string printName, Func<object[], object> body) =>
            lisp.Registered(printName).Value = new LispBuiltin(printName, 1, -1, body);

        #endregion

        #region Refusals

        /// <summary>
        /// COM to other programs is refused, as the rule of the presenter is (D228, D246): an error that names the function and
        /// the ProgID (a symbol is taken as its name), where AutoCAD gives nil for a ProgID it cannot find, so that a routine
        /// that starts Excel stops at once and its <c>*error*</c> says why.
        /// </summary>
        private static void DefineRefusals(LispInterpreter lisp)
        {
            foreach (string name in new[] { "vlax-create-object", "vlax-get-object", "vlax-get-or-create-object" })
            {
                string printed = name;
                lisp.Define(name.ToUpperInvariant(), 1, 1, a =>
                {
                    string progId =
                        a[0] is string text ? text
                        : a[0] is LispSymbol symbol ? symbol.Name
                        : throw LispInterpreter.TypeError("(or stringp symbolp):", a[0]);
                    throw new LispException(
                        printed
                            + ": COM to other programs is not supported by GeometryHelper.Cad: "
                            + LispInterpreter.Print(progId, true)
                    );
                });
            }

            lisp.Define("VLAX-IMPORT-TYPE-LIBRARY", 0, -1, a =>
                throw new LispException(
                    "vlax-import-type-library: type libraries are not supported by GeometryHelper.Cad"
                )
            );
        }

        #endregion
    }
}
