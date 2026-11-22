using System;
using System.Collections.Generic;

namespace GeometryHelper.Cad.Lisp
{
    /// <summary>The kinds of reactor Visual LISP makes, one for each <c>vlr-...-reactor</c> constructor.</summary>
    internal enum LispReactorKind
    {
        /// <summary>The database reactor: it hears of every object appended, modified or erased.</summary>
        Acdb,

        /// <summary>The command reactor: a command starts, ends or is cancelled, or an unknown one is typed.</summary>
        Command,

        /// <summary>The deep clone reactor (events not fired).</summary>
        DeepClone,

        /// <summary>The document manager reactor (events not fired).</summary>
        DocManager,

        /// <summary>The drawing reactor: a drawing is saved or closed.</summary>
        Dwg,

        /// <summary>The DXF reactor (events not fired).</summary>
        Dxf,

        /// <summary>The editor reactor: every event of the command, LISP, system variable and drawing reactors.</summary>
        Editor,

        /// <summary>The insert reactor (events not fired).</summary>
        Insert,

        /// <summary>The linker reactor (events not fired).</summary>
        Linker,

        /// <summary>The LISP reactor: an expression starts, ends or is cancelled.</summary>
        Lisp,

        /// <summary>The miscellaneous reactor (events not fired).</summary>
        Miscellaneous,

        /// <summary>The mouse reactor (events not fired).</summary>
        Mouse,

        /// <summary>The object reactor: the objects it owns are modified, erased or unerased.</summary>
        Object,

        /// <summary>The system variable reactor: a variable is about to change and has changed.</summary>
        Sysvar,

        /// <summary>The toolbar reactor (events not fired).</summary>
        Toolbar,

        /// <summary>The undo reactor (events not fired).</summary>
        Undo,

        /// <summary>The wblock reactor (events not fired).</summary>
        Wblock,

        /// <summary>The window reactor (events not fired).</summary>
        Window,

        /// <summary>The xref reactor (events not fired).</summary>
        Xref,
    }

    /// <summary>
    /// A reactor of Visual LISP, what <c>vlr-command-reactor</c> and its eighteen sisters make: a list of reactions (an event
    /// name and the function that answers it), a datum the routine keeps with it, the objects it owns (an object reactor), and
    /// whether it is added, which a new one is. It prints as <c>#&lt;VLR-Command-Reactor&gt;</c>, with no address.
    /// </summary>
    internal sealed class LispReactor
    {
        private readonly List<KeyValuePair<LispSymbol, LispSymbol>> _reactions =
            new List<KeyValuePair<LispSymbol, LispSymbol>>();
        private readonly List<LispVlaObject> _owners = new List<LispVlaObject>();

        /// <summary>Makes a reactor, added.</summary>
        /// <param name="kind">The kind.</param>
        /// <param name="data">The datum it keeps; nil for none.</param>
        internal LispReactor(LispReactorKind kind, object data)
        {
            Kind = kind;
            Data = data;
            Added = true;
        }

        /// <summary>Gets the kind.</summary>
        internal LispReactorKind Kind { get; }

        /// <summary>Gets or sets the datum the routine keeps with the reactor (<c>vlr-data</c>, <c>vlr-data-set</c>).</summary>
        internal object Data { get; set; }

        /// <summary>Gets or sets whether the reactor is added, so that its events are answered: true when made, false after <c>vlr-remove</c>, true again after <c>vlr-add</c>.</summary>
        internal bool Added { get; set; }

        /// <summary>Gets or sets whether <c>vlr-pers</c> marked it persistent: kept for the session only, nothing is saved in the drawing.</summary>
        internal bool Persistent { get; set; }

        /// <summary>Gets or sets what <c>vlr-set-notification</c> set: null until it is, when <c>active-document-only</c> is what <c>vlr-notification</c> says.</summary>
        internal LispSymbol Notification { get; set; }

        /// <summary>Gets the reactions, the newest first: the symbol of the event name and the symbol of the function that answers it, which is looked up when the event comes.</summary>
        internal IReadOnlyList<KeyValuePair<LispSymbol, LispSymbol>> Reactions => _reactions;

        /// <summary>Gets the objects an object reactor owns, in the order they were added.</summary>
        internal IReadOnlyList<LispVlaObject> Owners => _owners;

        /// <summary>Gets how it prints: <c>#&lt;VLR-Command-Reactor&gt;</c>.</summary>
        internal string PrintForm => "#<" + LispReactions.TypeName(Kind) + ">";

        /// <summary>Gives the reactor a reaction: a name it has gets the new function where it stands, a new name goes first (as AutoCAD lists them).</summary>
        /// <param name="name">The event name, as the registered symbol.</param>
        /// <param name="callback">The symbol that names the function.</param>
        internal void SetReaction(LispSymbol name, LispSymbol callback)
        {
            for (int i = 0; i < _reactions.Count; i++)
            {
                if (ReferenceEquals(_reactions[i].Key, name))
                {
                    _reactions[i] = new KeyValuePair<LispSymbol, LispSymbol>(name, callback);
                    return;
                }
            }

            _reactions.Insert(0, new KeyValuePair<LispSymbol, LispSymbol>(name, callback));
        }

        /// <summary>Gets the reaction of an event: the symbol of its name and its callback.</summary>
        /// <param name="reaction">The event name in any case, with or without the colon.</param>
        /// <param name="name">The event name, as the registered symbol.</param>
        /// <param name="callback">The symbol that names the function.</param>
        /// <returns>True when the reactor answers the event.</returns>
        internal bool TryGetReaction(string reaction, out LispSymbol name, out LispSymbol callback)
        {
            string wanted = reaction.TrimStart(':');

            foreach (KeyValuePair<LispSymbol, LispSymbol> found in _reactions)
            {
                if (string.Equals(found.Key.Name.TrimStart(':'), wanted, StringComparison.OrdinalIgnoreCase))
                {
                    name = found.Key;
                    callback = found.Value;
                    return true;
                }
            }

            name = null;
            callback = null;
            return false;
        }

        /// <summary>Adds an owner; one already there (the same object, by handle) is not added twice.</summary>
        /// <param name="owner">The object.</param>
        /// <returns>True when it was added.</returns>
        internal bool AddOwner(LispVlaObject owner)
        {
            if (OwnerIndex(owner) >= 0)
            {
                return false;
            }

            _owners.Add(owner);
            return true;
        }

        /// <summary>Takes an owner out.</summary>
        /// <param name="owner">The object.</param>
        /// <returns>True when it was one.</returns>
        internal bool RemoveOwner(LispVlaObject owner)
        {
            int at = OwnerIndex(owner);

            if (at < 0)
            {
                return false;
            }

            _owners.RemoveAt(at);
            return true;
        }

        /// <summary>Tells whether an object of a handle is an owner.</summary>
        /// <param name="handle">The handle.</param>
        /// <returns>True when it is.</returns>
        internal bool Owns(string handle) => _owners.FindIndex(o => SameKey(o.Key, handle)) >= 0;

        private int OwnerIndex(LispVlaObject owner) => _owners.FindIndex(o => SameKey(o.Key, owner.Key));

        private static bool SameKey(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>One callback to run: the event it answers, the reactor, the function and the arguments it is given.</summary>
    internal sealed class LispReactionCall
    {
        /// <summary>Makes a call.</summary>
        /// <param name="reactor">The reactor.</param>
        /// <param name="reaction">The event name, as the registered symbol.</param>
        /// <param name="callback">The symbol that names the function.</param>
        /// <param name="arguments">The arguments, the reactor and the parameters, with the notifier first for an object reactor.</param>
        internal LispReactionCall(LispReactor reactor, LispSymbol reaction, LispSymbol callback, object[] arguments)
        {
            Reactor = reactor;
            Reaction = reaction;
            Callback = callback;
            Arguments = arguments;
        }

        /// <summary>Gets the reactor.</summary>
        internal LispReactor Reactor { get; }

        /// <summary>Gets the event name.</summary>
        internal LispSymbol Reaction { get; }

        /// <summary>Gets the symbol that names the function.</summary>
        internal LispSymbol Callback { get; }

        /// <summary>Gets the arguments.</summary>
        internal object[] Arguments { get; }
    }

    /// <summary>
    /// The names of the reactor kinds and of the events, as AutoCAD 2021 spells them and lists them (probe m1): the nineteen
    /// kinds, in the order <c>vlr-types</c> gives, and for each the reactions it accepts. Which of the events the presenter
    /// fires is the business of the session; a reaction in a kind's list that is never raised is stored all the same.
    /// </summary>
    internal static class LispReactions
    {
        private static readonly string[] TypeNames =
        {
            "VLR-AcDb-Reactor",
            "VLR-Command-Reactor",
            "VLR-DeepClone-Reactor",
            "VLR-DocManager-Reactor",
            "VLR-DWG-Reactor",
            "VLR-DXF-Reactor",
            "VLR-Editor-Reactor",
            "VLR-Insert-Reactor",
            "VLR-Linker-Reactor",
            "VLR-Lisp-Reactor",
            "VLR-Miscellaneous-Reactor",
            "VLR-Mouse-Reactor",
            "VLR-Object-Reactor",
            "VLR-SysVar-Reactor",
            "VLR-Toolbar-Reactor",
            "VLR-Undo-Reactor",
            "VLR-Wblock-Reactor",
            "VLR-Window-Reactor",
            "VLR-XREF-Reactor",
        };

        // The order vlr-types lists the kinds in.
        private static readonly LispReactorKind[] TypesOrder =
        {
            LispReactorKind.Linker,
            LispReactorKind.Editor,
            LispReactorKind.Acdb,
            LispReactorKind.DocManager,
            LispReactorKind.Command,
            LispReactorKind.Lisp,
            LispReactorKind.Dxf,
            LispReactorKind.Dwg,
            LispReactorKind.Insert,
            LispReactorKind.Wblock,
            LispReactorKind.Sysvar,
            LispReactorKind.DeepClone,
            LispReactorKind.Xref,
            LispReactorKind.Undo,
            LispReactorKind.Window,
            LispReactorKind.Toolbar,
            LispReactorKind.Mouse,
            LispReactorKind.Miscellaneous,
            LispReactorKind.Object,
        };

        // The reactions of each kind, in the order vlr-reaction-names lists them, spelled after the VLR- in front of them.
        private static readonly string[][] KindNames =
        {
            Words(
                "objectAppended objectUnAppended objectReAppended objectOpenedForModify objectModified objectErased objectUnErased"
            ),
            Words("unknownCommand commandWillStart commandEnded commandCancelled commandFailed"),
            Words("beginDeepClone beginDeepCloneXlation abortDeepClone endDeepClone"),
            Words(
                "documentCreated documentToBeDestroyed documentLockModeWillChange documentLockModeChangeVetoed documentLockModeChanged documentBecameCurrent documentToBeActivated documentToBeDeactivated"
            ),
            Words(
                "beginDwgOpen endDwgOpen dwgFileOpened databaseConstructed databaseToBeDestroyed beginSave saveComplete beginClose"
            ),
            Words("beginDxfIn abortDxfIn dxfInComplete beginDxfOut abortDxfOut dxfOutComplete"),
            Words(
                "unknownCommand commandWillStart commandEnded commandCancelled commandFailed lispWillStart lispEnded lispCancelled beginClose beginDxfIn abortDxfIn dxfInComplete beginDxfOut abortDxfOut dxfOutComplete beginDwgOpen endDwgOpen dwgFileOpened databaseConstructed databaseToBeDestroyed beginSave saveComplete sysVarWillChange sysVarChanged"
            ),
            Words("beginInsert beginInsertM otherInsert abortInsert endInsert"),
            Words("rxAppLoaded rxAppUnLoaded"),
            Words("lispWillStart lispEnded lispCancelled"),
            Words("pickfirstModified layoutSwitched"),
            Words("beginDoubleClick beginRightClick"),
            Words(
                "cancelled copied erased unerased goodbye openedForModify modified subObjModified modifyUndone modifiedXData unappended reappended objectClosed"
            ),
            Words("sysVarWillChange sysVarChanged"),
            Words("toolbarBitmapSizeWillChange toolbarBitmapSizeChanged"),
            Words(
                "undoSubcommandAuto undoSubcommandControl undoSubcommandBegin undoSubcommandEnd undoSubcommandMark undoSubcommandBack undoSubcommandNumber"
            ),
            Words(
                "wblockNotice beginWblockPt beginWblockId beginWblock otherWblock abortWblock endWblock beginWblockObjects"
            ),
            Words("docFrameMovedOrResized mainFrameMovedOrResized"),
            Words(
                "beginAttach otherAttach abortAttach endAttach redirected comandeered beginRestore abortRestore endRestore xrefSubcommandBindItem xrefSubcommandAttachItem xrefSubcommandOverlayItem xrefSubcommandDetachItem xrefSubcommandPathItem xrefSubcommandReloadItem xrefSubcommandUnloadItem"
            ),
        };

        private static string[] Words(string text) => text.Split(' ');

        /// <summary>Gets the name of a kind: <c>VLR-Command-Reactor</c>.</summary>
        /// <param name="kind">The kind.</param>
        /// <returns>The name, as it prints and as the keyword is spelled.</returns>
        internal static string TypeName(LispReactorKind kind) => TypeNames[(int)kind];

        /// <summary>Gets the kinds in the order <c>vlr-types</c> lists them.</summary>
        internal static IReadOnlyList<LispReactorKind> AllKinds => TypesOrder;

        /// <summary>Gets the kind a type keyword names, in any case.</summary>
        /// <param name="keyword">The keyword's name with its colon, as the symbol has it.</param>
        /// <param name="kind">The kind.</param>
        /// <returns>True when it is one of the nineteen.</returns>
        internal static bool TryKind(string keyword, out LispReactorKind kind)
        {
            if (keyword.Length > 1 && keyword[0] == ':')
            {
                for (int i = 0; i < TypeNames.Length; i++)
                {
                    if (string.Equals(TypeNames[i], keyword.Substring(1), StringComparison.OrdinalIgnoreCase))
                    {
                        kind = (LispReactorKind)i;
                        return true;
                    }
                }
            }

            kind = default;
            return false;
        }

        /// <summary>Gets every type keyword and every reaction name, each once, spelled with its colon (<c>:VLR-commandWillStart</c>), so that a symbol can be registered for each.</summary>
        /// <returns>The names.</returns>
        internal static IEnumerable<string> AllKeywords()
        {
            foreach (string type in TypeNames)
            {
                yield return ":" + type;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (string[] names in KindNames)
            {
                foreach (string name in names)
                {
                    if (seen.Add(name))
                    {
                        yield return ":VLR-" + name;
                    }
                }
            }
        }

        /// <summary>Gets the reactions a kind accepts, spelled with the colon, in the order <c>vlr-reaction-names</c> lists them.</summary>
        /// <param name="kind">The kind.</param>
        /// <returns>The names.</returns>
        internal static IEnumerable<string> NamesOf(LispReactorKind kind)
        {
            foreach (string name in KindNames[(int)kind])
            {
                yield return ":VLR-" + name;
            }
        }

        /// <summary>Tells whether a kind accepts a reaction, which is also whether a reactor of the kind answers the event when the presenter raises it.</summary>
        /// <param name="kind">The kind.</param>
        /// <param name="reaction">The event name in any case, with or without the colon.</param>
        /// <returns>True when the kind's list has it.</returns>
        internal static bool Accepts(LispReactorKind kind, string reaction)
        {
            string wanted = reaction.TrimStart(':');

            foreach (string name in KindNames[(int)kind])
            {
                if (string.Equals("VLR-" + name, wanted, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
