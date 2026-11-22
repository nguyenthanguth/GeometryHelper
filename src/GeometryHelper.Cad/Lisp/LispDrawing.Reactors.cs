using System.Collections.Generic;

namespace GeometryHelper.Cad.Lisp
{
    /// <summary>
    /// The reactors of Visual LISP: the nineteen <c>vlr-...-reactor</c> constructors and the functions that read and change a
    /// reactor, as AutoCAD 2021 gives them (probe m1), <c>vl-load-reactors</c> and <c>vlax-queueexpr</c>. The constructors make a
    /// reactor and add it; the session raises the events (<see cref="CadCommandSession"/>), and the interpreter runs the callbacks
    /// (<see cref="LispInterpreter.RunReactions"/>). The functions exist from the start, where AutoCAD has them only after
    /// <c>(vl-load-com)</c>: the keywords they take (<c>:vlr-command-reactor</c>, <c>:vlr-commandWillStart</c>) are bound to
    /// themselves from the start too, so that they need no quote.
    /// </summary>
    internal static partial class LispDrawing
    {
        private const string NotificationActive = "active-document-only";

        static partial void RegisterReactors(LispInterpreter lisp, CadCommandSession session)
        {
            foreach (string keyword in LispReactions.AllKeywords())
            {
                LispSymbol symbol = lisp.Registered(keyword);
                symbol.Value = symbol;
            }

            foreach (LispReactorKind kind in LispReactions.AllKinds)
            {
                LispReactorKind made = kind;
                string name = "VLR-" + LispReactions.TypeName(kind).Substring(4).ToUpperInvariant();

                if (kind == LispReactorKind.Object)
                {
                    lisp.Define(name, 0, 3, a => MakeReactor(lisp, session, made, a.Length > 0 ? a[0] : null, 1, a));
                }
                else
                {
                    lisp.Define(name, 0, 2, a => MakeReactor(lisp, session, made, null, 0, a));
                }
            }

            lisp.Define("VLR-ADD", 1, 1, a => lisp.OnOwner(() => Add(lisp, session, ReactorOf(a[0]))));
            lisp.Define("VLR-REMOVE", 1, 1, a => lisp.OnOwner(() => Remove(lisp, session, ReactorOf(a[0]))));
            lisp.Define("VLR-ADDED-P", 1, 1, a => lisp.Bool(ReactorOf(a[0]).Added));
            lisp.Define("VLR-REMOVE-ALL", 0, 1, a =>
                lisp.OnOwner(() => RemoveAll(lisp, session, a.Length > 0 ? KindOf(a[0]) : (LispReactorKind?)null))
            );
            lisp.Define("VLR-REACTORS", 0, -1, a =>
            {
                var kinds = new List<LispReactorKind>();

                foreach (object type in a)
                {
                    kinds.Add(KindOf(type));
                }

                return lisp.OnOwner(() => Groups(lisp, session.AddedReactors, kinds));
            });
            lisp.Define("VLR-TYPE", 1, 1, a => KindSymbol(lisp, ReactorOf(a[0]).Kind));
            lisp.Define("VLR-TYPES", 0, 0, a =>
            {
                var types = new List<object>();

                foreach (LispReactorKind kind in LispReactions.AllKinds)
                {
                    types.Add(KindSymbol(lisp, kind));
                }

                return LispInterpreter.ListOf(types);
            });
            lisp.Define("VLR-DATA", 1, 1, a => ReactorOf(a[0]).Data);
            lisp.Define("VLR-DATA-SET", 2, 2, a =>
            {
                ReactorOf(a[0]).Data = a[1];
                return a[1];
            });
            lisp.Define("VLR-REACTIONS", 1, 1, a =>
            {
                var pairs = new List<object>();

                foreach (KeyValuePair<LispSymbol, LispSymbol> reaction in ReactorOf(a[0]).Reactions)
                {
                    pairs.Add(new LispCons(reaction.Key, reaction.Value));
                }

                return LispInterpreter.ListOf(pairs);
            });
            lisp.Define("VLR-REACTION-SET", 3, 3, a => ReactionSet(lisp, ReactorOf(a[0]), a[1], a[2]));
            lisp.Define("VLR-REACTION-NAMES", 1, 1, a => ReactionNames(lisp, a[0]));
            lisp.Define("VLR-CURRENT-REACTION-NAME", 0, 0, a => lisp.ReactionName);

            lisp.Define("VLR-OWNERS", 1, 1, a =>
            {
                var owners = new List<object>();

                foreach (LispVlaObject owner in ObjectReactorOf(a[0]).Owners)
                {
                    owners.Add(owner);
                }

                return LispInterpreter.ListOf(owners);
            });
            lisp.Define("VLR-OWNER-ADD", 2, 2, a =>
            {
                LispReactor reactor = ObjectReactorOf(a[0]);
                reactor.AddOwner(OwnerOf(a[1]));
                return reactor;
            });
            lisp.Define("VLR-OWNER-REMOVE", 2, 2, a =>
            {
                LispReactor reactor = ObjectReactorOf(a[0]);
                reactor.RemoveOwner(OwnerOf(a[1]));
                return reactor;
            });

            // Persistence is a mark kept for the session: nothing is written to the drawing and nothing is read back (D1464).
            lisp.Define("VLR-PERS", 1, 1, a =>
            {
                LispReactor reactor = ReactorOf(a[0]);
                reactor.Persistent = true;
                return reactor;
            });
            lisp.Define("VLR-PERS-P", 1, 1, a =>
                ReactorOf(a[0]) is LispReactor reactor && reactor.Persistent ? reactor : null
            );
            lisp.Define("VLR-PERS-RELEASE", 1, 1, a =>
            {
                LispReactor reactor = ReactorOf(a[0]);
                reactor.Persistent = false;
                return reactor;
            });
            lisp.Define("VLR-PERS-LIST", 0, 1, a =>
            {
                if (a.Length > 0)
                {
                    ReactorOf(a[0]);
                }

                return lisp.OnOwner(() => PersistentList(session.AddedReactors));
            });
            lisp.Define("VLR-PERS-ACTIVATE", 0, -1, a => null);
            lisp.Define("VLR-PERS-DICTNAME", 0, -1, a => "VL-REACTORS");

            lisp.Define("VLR-NOTIFICATION", 1, 1, a =>
                ReactorOf(a[0]).Notification ?? new LispSymbol(NotificationActive)
            );
            lisp.Define("VLR-SET-NOTIFICATION", 2, 2, a => SetNotification(lisp, ReactorOf(a[0]), a[1]));
            lisp.Define("VLR-DOCUMENT", 1, 1, a =>
            {
                ReactorOf(a[0]);
                return null;
            });
            lisp.Define("VLR-TRACE-REACTION", 0, -1, a =>
            {
                lisp.WriteFreshLine();
                lisp.Write(
                    "; Reaction: "
                        + LispInterpreter.Print(lisp.ReactionName, true)
                        + "; argument list: "
                        + LispInterpreter.Print(LispInterpreter.ListOf(a), true)
                        + "\n"
                );
                return null;
            });
            lisp.Define("VLR-BEEP-REACTION", 0, -1, a => null);

            lisp.Define("VL-LOAD-REACTORS", 0, 0, a => lisp.EmptySymbol);
            lisp.Define("VLAX-QUEUEEXPR", 1, 1, a =>
            {
                LispInterpreter.Str(a[0]);
                return null;
            });
        }

        /// <summary>The reactor an argument is: a <see cref="LispReactor"/>, else AutoCAD's <c>bad argument type: VLReactor: 5</c>.</summary>
        private static LispReactor ReactorOf(object value) =>
            value as LispReactor ?? throw LispInterpreter.TypeError("VLReactor", value, colon: true);

        /// <summary>The object reactor an argument is; any other reactor is AutoCAD's <c>bad argument type: Object reactor: #&lt;VLR-Command-Reactor&gt;</c>.</summary>
        private static LispReactor ObjectReactorOf(object value)
        {
            LispReactor reactor = ReactorOf(value);
            return reactor.Kind == LispReactorKind.Object
                ? reactor
                : throw new LispException("bad argument type: Object reactor: " + reactor.PrintForm);
        }

        /// <summary>The kind a type keyword names; <c>unknown reactor type: nil</c> for what is none (a keyword AutoCAD does not have evaluates to nil).</summary>
        private static LispReactorKind KindOf(object value) =>
            value is LispSymbol symbol && LispReactions.TryKind(symbol.Name, out LispReactorKind kind)
                ? kind
                : throw new LispException("unknown reactor type: " + LispInterpreter.Print(value, true));

        private static LispSymbol KindSymbol(LispInterpreter lisp, LispReactorKind kind) =>
            lisp.Registered(":" + LispReactions.TypeName(kind));

        /// <summary>The owner an argument is: an object of the drawing, else AutoCAD's <c>parameter is not a valid dispatch interface (IDispatch) argument: 1</c>.</summary>
        private static LispVlaObject OwnerOf(object value) =>
            value as LispVlaObject
            ?? throw new LispException(
                "parameter is not a valid dispatch interface (IDispatch) argument: "
                    + LispInterpreter.Print(value, true)
            );

        /// <summary>
        /// <c>(vlr-command-reactor data callbacks)</c> and its sisters, and <c>(vlr-object-reactor owners data callbacks)</c>:
        /// makes a reactor and adds it. The callbacks are a list of <c>(:vlr-name . function)</c> pairs, each name one the kind
        /// accepts (<c>invalid reaction name: :VLR-modified</c>) and each function a symbol or a string (<c>bad argument type: (or
        /// stringp symbolp): 5</c>); the reactions are kept the last given first, and a name given twice keeps the last.
        /// </summary>
        private static object MakeReactor(
            LispInterpreter lisp,
            CadCommandSession session,
            LispReactorKind kind,
            object owners,
            int dataAt,
            object[] a
        )
        {
            var ownerObjects = new List<LispVlaObject>();

            if (kind == LispReactorKind.Object)
            {
                if (owners != null && !(owners is LispCons))
                {
                    throw LispInterpreter.TypeError("consp", owners);
                }

                foreach (object owner in LispInterpreter.ItemsOf(owners))
                {
                    ownerObjects.Add(OwnerOf(owner));
                }
            }

            object data = a.Length > dataAt ? a[dataAt] : null;
            object callbacks = a.Length > dataAt + 1 ? a[dataAt + 1] : null;

            if (callbacks != null && !(callbacks is LispCons))
            {
                throw LispInterpreter.TypeError("consp", callbacks);
            }

            var reactor = new LispReactor(kind, data);

            foreach (object item in LispInterpreter.ItemsOf(callbacks))
            {
                if (!(item is LispCons pair))
                {
                    throw LispInterpreter.TypeError("consp", item);
                }

                reactor.SetReaction(ReactionName(kind, pair.Car), CallbackName(lisp, pair.Cdr));
            }

            foreach (LispVlaObject owner in ownerObjects)
            {
                reactor.AddOwner(owner);
            }

            return lisp.OnOwner(() =>
            {
                session.AddReactor(reactor);
                return reactor;
            });
        }

        /// <summary>The symbol of a reaction name a kind accepts, given as its keyword; <c>invalid reaction name: ...</c> for any other.</summary>
        private static LispSymbol ReactionName(LispReactorKind kind, object name) =>
            name is LispSymbol symbol
            && symbol.Name.StartsWith(":", System.StringComparison.Ordinal)
            && LispReactions.Accepts(kind, symbol.Name)
                ? symbol
                : throw new LispException("invalid reaction name: " + LispInterpreter.Print(name, true));

        /// <summary>The symbol that names a callback: a symbol as it is, a string as the symbol of its upper-case name; else AutoCAD's <c>bad argument type: (or stringp symbolp): 5</c>.</summary>
        private static LispSymbol CallbackName(LispInterpreter lisp, object callback)
        {
            if (callback is LispSymbol symbol)
            {
                return symbol;
            }

            if (callback is string text)
            {
                return lisp.Intern(text.ToUpperInvariant());
            }

            throw LispInterpreter.TypeError("(or stringp symbolp)", callback, colon: true);
        }

        /// <summary>
        /// <c>(vlr-reaction-set reactor name function)</c>: sets or replaces one reaction and gives the function's name as an upper-case
        /// string; a function that is nil is <c>invalid reaction function name: nil</c>, a name the kind does not accept
        /// <c>invalid reaction name: ...</c>.
        /// </summary>
        private static object ReactionSet(LispInterpreter lisp, LispReactor reactor, object name, object callback)
        {
            LispSymbol reaction =
                name is LispSymbol symbol
                && symbol.Name.StartsWith(":", System.StringComparison.Ordinal)
                && LispReactions.Accepts(reactor.Kind, symbol.Name)
                    ? symbol
                    : throw new LispException("invalid reaction name: " + LispInterpreter.Print(name, true));

            if (!(callback is LispSymbol function) && !(callback is string))
            {
                throw new LispException("invalid reaction function name: " + LispInterpreter.Print(callback, true));
            }

            LispSymbol named = CallbackName(lisp, callback);
            reactor.SetReaction(reaction, named);
            return named.Name;
        }

        /// <summary><c>(vlr-reaction-names reactor-or-type)</c>: the reactions a kind accepts, as keywords, in AutoCAD's order.</summary>
        private static object ReactionNames(LispInterpreter lisp, object what)
        {
            LispReactorKind kind;

            if (what is LispReactor reactor)
            {
                kind = reactor.Kind;
            }
            else if (!(what is LispSymbol symbol && LispReactions.TryKind(symbol.Name, out kind)))
            {
                throw new LispException(
                    "bad argument type: must be reactor object or reactor type keyword: "
                        + LispInterpreter.Print(what, true)
                );
            }

            var names = new List<object>();

            foreach (string name in LispReactions.NamesOf(kind))
            {
                names.Add(lisp.Registered(name));
            }

            return LispInterpreter.ListOf(names);
        }

        /// <summary>Adds a reactor that is not added; gives it back, or nil for one that was.</summary>
        private static object Add(LispInterpreter lisp, CadCommandSession session, LispReactor reactor)
        {
            if (reactor.Added)
            {
                return null;
            }

            session.AddReactor(reactor);
            return reactor;
        }

        /// <summary>Takes a reactor out; gives it back, or nil for one that was not added.</summary>
        private static object Remove(LispInterpreter lisp, CadCommandSession session, LispReactor reactor)
        {
            if (!reactor.Added)
            {
                return null;
            }

            session.RemoveReactor(reactor);
            return reactor;
        }

        /// <summary>Takes out every reactor, or every one of a kind, and gives what it took out in the shape of <c>vlr-reactors</c>; nil where there was none.</summary>
        private static object RemoveAll(LispInterpreter lisp, CadCommandSession session, LispReactorKind? kind)
        {
            var kinds = new List<LispReactorKind>();

            if (kind.HasValue)
            {
                kinds.Add(kind.Value);
            }

            var taken = new List<LispReactor>(session.AddedReactors);
            object groups = Groups(lisp, taken, kinds);

            foreach (LispReactor reactor in taken)
            {
                if (!kind.HasValue || reactor.Kind == kind.Value)
                {
                    session.RemoveReactor(reactor);
                }
            }

            return groups;
        }

        /// <summary>
        /// The reactors as <c>vlr-reactors</c> lists them: a list of groups, each the kind's keyword and then its reactors, the
        /// newest reactor first, and the kind of the newest reactor first; the kinds asked for, or every kind where none is; nil
        /// where there is no reactor.
        /// </summary>
        private static object Groups(
            LispInterpreter lisp,
            IReadOnlyList<LispReactor> added,
            List<LispReactorKind> kinds
        )
        {
            var order = new List<LispReactorKind>();
            var members = new Dictionary<LispReactorKind, List<object>>();

            for (int i = added.Count - 1; i >= 0; i--)
            {
                LispReactor reactor = added[i];

                if (kinds.Count > 0 && !kinds.Contains(reactor.Kind))
                {
                    continue;
                }

                if (!members.TryGetValue(reactor.Kind, out List<object> group))
                {
                    members[reactor.Kind] = group = new List<object> { KindSymbol(lisp, reactor.Kind) };
                    order.Add(reactor.Kind);
                }

                group.Add(reactor);
            }

            var groups = new List<object>();

            foreach (LispReactorKind kind in order)
            {
                groups.Add(LispInterpreter.ListOf(members[kind]));
            }

            return LispInterpreter.ListOf(groups);
        }

        /// <summary>The persistent reactors among those added, newest first, as a flat list; nil for none.</summary>
        private static object PersistentList(IReadOnlyList<LispReactor> added)
        {
            var persistent = new List<object>();

            for (int i = added.Count - 1; i >= 0; i--)
            {
                if (added[i].Persistent)
                {
                    persistent.Add(added[i]);
                }
            }

            return LispInterpreter.ListOf(persistent);
        }

        /// <summary><c>(vlr-set-notification reactor 'all-documents)</c>: the symbols <c>all-documents</c> and <c>active-document-only</c> are taken, any other value is <c>Incorrect notification value: ...</c>.</summary>
        private static object SetNotification(LispInterpreter lisp, LispReactor reactor, object value)
        {
            if (
                value is LispSymbol symbol
                && !symbol.Name.StartsWith(":", System.StringComparison.Ordinal)
                && (symbol.Name == "ALL-DOCUMENTS" || symbol.Name == "ACTIVE-DOCUMENT-ONLY")
            )
            {
                reactor.Notification = symbol;
                return reactor;
            }

            throw new LispException("Incorrect notification value: " + LispInterpreter.Print(value, true));
        }
    }
}
