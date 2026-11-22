using System;
using System.Collections.Generic;
using GeometryHelper.Cad.Lisp;
using GeometryHelper.Dxf;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// The reactors of Visual LISP of a document: the ones its routines made (<c>vlr-command-reactor</c> and the others) and the
    /// events the session raises for them, which the document's own events (a drawing saved, an object changed) join. A
    /// document with no reactor pays for nothing here: every raise looks at one null field first.
    /// </summary>
    public sealed partial class CadCommandSession
    {
        // The reactors added, in the order they were added; null until a routine makes the first, which also hooks the
        // document's events.
        private List<LispReactor> _addedReactors;
        private LispActiveX _reactorObjects;

        // How many vlr-lispEnded are still to come: the forms of the evaluation that started last.
        private int _lispReactForms;

        /// <summary>Gets the reactors added, in the order they were added.</summary>
        internal IReadOnlyList<LispReactor> AddedReactors =>
            _addedReactors ?? (IReadOnlyList<LispReactor>)Array.Empty<LispReactor>();

        /// <summary>Adds a reactor, which is answered from now on: the first one hooks the document's events.</summary>
        /// <param name="reactor">The reactor, not added yet.</param>
        internal void AddReactor(LispReactor reactor)
        {
            if (_addedReactors == null)
            {
                _addedReactors = new List<LispReactor>();
                Document.Changed += OnDocumentChangedForReactors;
                Document.Saving += OnDocumentSavingForReactors;
            }

            reactor.Added = true;
            _addedReactors.Add(reactor);
        }

        /// <summary>Takes a reactor out: it is not answered until it is added again.</summary>
        /// <param name="reactor">The reactor.</param>
        internal void RemoveReactor(LispReactor reactor)
        {
            reactor.Added = false;
            _addedReactors?.Remove(reactor);
        }

        /// <summary>Gets the objects reactors are told of as values of AutoLISP.</summary>
        private LispActiveX ReactorObjects =>
            _reactorObjects ?? (_reactorObjects = new LispActiveX(Lisp, this, LispDrawing.StateOf(Lisp)));

        /// <summary>The value of the entity of a handle: the entity, an erased one too (its use is the error), or a plain one where nothing is known of it.</summary>
        internal LispVlaObject ReactorObject(string handle)
        {
            DxfEntity entity = Document.FindEntity(handle) ?? Document.FindKept(handle);
            return entity != null
                ? ReactorObjects.WrapEntity(entity.Item)
                : ReactorObjects.Make(LispVlaKind.Entity, "IAcadEntity", handle);
        }

        /// <summary>
        /// Raises an event for the reactors that answer it: each reactor added that has a reaction of the name and is of a kind
        /// that hears it is called with itself and the parameters, a list. Where no evaluation goes on, the callbacks run at
        /// once as an evaluation of their own with no echo; during one they are queued for the worker (D1462).
        /// </summary>
        /// <param name="reaction">The event name: <c>vlr-commandWillStart</c>.</param>
        /// <param name="parameters">The parameters the callbacks get as a list.</param>
        private void RaiseReaction(string reaction, params object[] parameters)
        {
            if (_addedReactors == null || _addedReactors.Count == 0)
            {
                return;
            }

            List<LispReactionCall> calls = null;

            foreach (LispReactor reactor in _addedReactors.ToArray())
            {
                if (reactor.TryGetReaction(reaction, out LispSymbol name, out LispSymbol callback))
                {
                    (calls ?? (calls = new List<LispReactionCall>())).Add(
                        new LispReactionCall(
                            reactor,
                            name,
                            callback,
                            new object[] { reactor, LispInterpreter.ListOf(parameters) }
                        )
                    );
                }
            }

            Deliver(calls);
        }

        /// <summary>Hands callbacks to where they run: the queue of the evaluation going on, or at once.</summary>
        private void Deliver(List<LispReactionCall> calls)
        {
            if (calls == null || calls.Count == 0)
            {
                return;
            }

            if (_lisp.IsEvaluating)
            {
                _lisp.QueueReactions(calls);
                return;
            }

            // Changes the callbacks make where no command runs are one step named after the event; inside a command they join
            // its step.
            bool ownsStep = !_lispStep;
            string name = _lispStepName;

            if (ownsStep)
            {
                _lispStepName = calls[0].Reaction.Name.TrimStart(':');
            }

            LispResult result;

            try
            {
                result = _lisp.RunReactions(calls);
            }
            finally
            {
                if (ownsStep)
                {
                    CloseLispStep();
                    _lispStepName = name;
                }
            }

            if (!string.IsNullOrEmpty(result.Output))
            {
                foreach (string line in result.Output.Split('\n'))
                {
                    Write(line);
                }
            }

            Changed();
        }

        /// <summary>Whether the command is one of the user's: not the carrier of a routine's question, nor the implied window, nor a grip edit.</summary>
        private bool IsReportedCommand(CadCommand command) =>
            command != null
            && !ReferenceEquals(command, _lispCommand)
            && !ReferenceEquals(command, _windowCommand)
            && !ReferenceEquals(command, _gripCommand);

        /// <summary>Raises <c>vlr-commandWillStart</c> for a command about to ask its first prompt.</summary>
        private void RaiseCommandStarts(CadCommand command)
        {
            if (_addedReactors != null && IsReportedCommand(command))
            {
                RaiseReaction("vlr-commandWillStart", command.Name);
            }
        }

        /// <summary>Raises <c>vlr-commandEnded</c>, <c>vlr-commandCancelled</c> or <c>vlr-commandFailed</c> for a command that has ended.</summary>
        private void RaiseCommandEnds(CadCommand command, CadCommandOutcome outcome)
        {
            if (_addedReactors != null && IsReportedCommand(command))
            {
                RaiseReaction(
                    outcome == CadCommandOutcome.Completed ? "vlr-commandEnded"
                        : outcome == CadCommandOutcome.Cancelled ? "vlr-commandCancelled"
                        : "vlr-commandFailed",
                    command.Name
                );
            }
        }

        /// <summary>Raises <c>vlr-unknownCommand</c> for a name typed that is no command.</summary>
        private void RaiseUnknownCommand(string name)
        {
            if (_addedReactors != null)
            {
                RaiseReaction("vlr-unknownCommand", name);
            }
        }

        /// <summary>
        /// Raises <c>vlr-lispWillStart</c> for the expression about to be evaluated: once for each top-level form of the text, with
        /// the first line of the form as typed (AutoCAD 2021, probe m1: <c>("(+ 1 2)")</c>, and <c>("(progn")</c> for a form broken
        /// over two lines). Notes how many, so that the same number of <c>vlr-lispEnded</c> follow.
        /// </summary>
        /// <param name="source">The text of the evaluation: a line typed, a <c>(load ...)</c>, <c>(C:NAME)</c>.</param>
        private void RaiseLispStarts(string source)
        {
            // A line the last evaluation left open is closed by the one that starts.
            _openLine = -1;
            List<string> forms = LispFormTexts(source);
            _lispReactForms = forms.Count;

            if (_addedReactors != null)
            {
                foreach (string form in forms)
                {
                    RaiseReaction("vlr-lispWillStart", form);
                }
            }
        }

        /// <summary>
        /// Raises <c>vlr-lispEnded</c> for each form that started, with no parameters, after the evaluation ends, with an error or
        /// not; where the user's Esc ended a get function (<c>Function cancelled</c>) <c>vlr-lispCancelled</c> comes first. Does
        /// nothing for an evaluation that was not started by <see cref="RaiseLispStarts"/>.
        /// </summary>
        /// <param name="result">What the evaluation gave.</param>
        private void RaiseLispEnds(LispResult result)
        {
            int forms = _lispReactForms;
            _lispReactForms = 0;

            if (forms == 0 || _addedReactors == null)
            {
                return;
            }

            if (_lisp.TakeCancelledAtAsk())
            {
                RaiseReaction("vlr-lispCancelled");
            }

            for (int i = 0; i < forms; i++)
            {
                RaiseReaction("vlr-lispEnded");
            }
        }

        /// <summary>The first lines of the top-level forms of a text: a list in parentheses, a string or an atom, with the quote in front of it.</summary>
        private static List<string> LispFormTexts(string source)
        {
            var forms = new List<string>();
            int i = 0;

            while (i < source.Length)
            {
                while (i < source.Length && char.IsWhiteSpace(source[i]))
                {
                    i++;
                }

                if (i < source.Length && source[i] == ';')
                {
                    while (i < source.Length && source[i] != '\n')
                    {
                        i++;
                    }

                    continue;
                }

                int start = i;
                int depth = 0;
                bool inString = false;

                while (i < source.Length)
                {
                    char c = source[i];

                    if (inString)
                    {
                        i += c == '\\' ? 2 : 1;
                        inString = c != '"';
                        continue;
                    }

                    if (c == '"')
                    {
                        inString = true;
                    }
                    else if (c == '(')
                    {
                        depth++;
                    }
                    else if (c == ')')
                    {
                        depth--;
                    }
                    else if (depth <= 0 && char.IsWhiteSpace(c))
                    {
                        break;
                    }

                    i++;

                    if (depth == 0 && !inString && c == ')')
                    {
                        break;
                    }
                }

                string form = source.Substring(start, Math.Min(i, source.Length) - start);
                int lineEnd = form.IndexOf('\n');
                form = (lineEnd < 0 ? form : form.Substring(0, lineEnd)).TrimEnd('\r');

                if (form.Length > 0)
                {
                    forms.Add(form);
                }
            }

            if (forms.Count == 0)
            {
                forms.Add(source.Trim());
            }

            return forms;
        }

        /// <summary>Raises <c>vlr-sysVarWillChange</c> for a variable about to be set.</summary>
        /// <param name="name">The variable's name.</param>
        internal void RaiseSysVarWillChange(string name)
        {
            if (_addedReactors != null)
            {
                RaiseReaction("vlr-sysVarWillChange", name);
            }
        }

        /// <summary>Raises <c>vlr-sysVarChanged</c> for a variable that was set.</summary>
        /// <param name="name">The variable's name.</param>
        /// <param name="taken">Whether the value was taken.</param>
        internal void RaiseSysVarChanged(string name, bool taken)
        {
            if (_addedReactors != null)
            {
                RaiseReaction("vlr-sysVarChanged", name, _lisp.Bool(taken));
            }
        }

        /// <summary>Raises <c>vlr-beginClose</c>, as the window does just before it lets the document go.</summary>
        internal void RaiseClosing()
        {
            if (_addedReactors != null)
            {
                RaiseReaction("vlr-beginClose");
            }
        }

        private void OnDocumentSavingForReactors(object sender, string path) => RaiseReaction("vlr-beginSave", path);

        private void OnDocumentChangedForReactors(object sender, CadDocumentChangedEventArgs e)
        {
            if (e.Kind == CadChangeKind.Saved)
            {
                RaiseReaction("vlr-saveComplete", Document.FileName);
                return;
            }

            if (
                e.Records == null
                || (e.Kind != CadChangeKind.Edit && e.Kind != CadChangeKind.Undo && e.Kind != CadChangeKind.Redo)
                || !_addedReactors.Exists(r => r.Kind == LispReactorKind.Acdb || r.Kind == LispReactorKind.Object)
            )
            {
                return;
            }

            var calls = new List<LispReactionCall>();

            foreach (KeyValuePair<string, string> happened in WhatHappened(e))
            {
                CollectObjectCalls(calls, happened.Value, happened.Key);
            }

            Deliver(calls);
        }

        /// <summary>
        /// What happened to each entity a change touched, by handle in ordinal order, as its records tell: an entity added is
        /// <c>appended</c>, one erased <c>erased</c>, any other change <c>modified</c>; undoing a step turns an add into an erase
        /// and an erase into an unerase, and redoing it turns an add into an unerase (AutoCAD 2021, probe m1: ERASE gives one
        /// objectErased, U one objectUnErased). An entity both added and erased by the change is erased.
        /// </summary>
        private static SortedDictionary<string, string> WhatHappened(CadDocumentChangedEventArgs e)
        {
            var happened = new SortedDictionary<string, string>(StringComparer.Ordinal);

            foreach (CadEditRecord record in e.Records)
            {
                if (record.Handle == null)
                {
                    continue;
                }

                string what;

                switch (record.Kind)
                {
                    case CadEditRecordKind.Added:
                        what =
                            e.Kind == CadChangeKind.Undo ? "erased"
                            : e.Kind == CadChangeKind.Redo ? "unerased"
                            : "appended";
                        break;
                    case CadEditRecordKind.Erased:
                        what = e.Kind == CadChangeKind.Undo ? "unerased" : "erased";
                        break;
                    default:
                        what = "modified";
                        break;
                }

                if (!happened.TryGetValue(record.Handle, out string before) || before == "modified" || what == "erased")
                {
                    happened[record.Handle] = what;
                }
            }

            return happened;
        }

        /// <summary>Collects the callbacks of the reactors that answer what happened to an object.</summary>
        private void CollectObjectCalls(List<LispReactionCall> calls, string what, string handle)
        {
            string acdb =
                what == "unerased"
                    ? "vlr-objectUnErased"
                    : "vlr-object" + char.ToUpperInvariant(what[0]) + what.Substring(1);
            LispVlaObject notifier = null;

            foreach (LispReactor reactor in _addedReactors.ToArray())
            {
                if (
                    reactor.Kind == LispReactorKind.Acdb
                    && reactor.TryGetReaction(acdb, out LispSymbol name, out LispSymbol callback)
                )
                {
                    notifier = notifier ?? ReactorObject(handle);
                    calls.Add(
                        new LispReactionCall(
                            reactor,
                            name,
                            callback,
                            new object[] { reactor, LispInterpreter.ListOf(new object[] { notifier }) }
                        )
                    );
                }
                else if (
                    reactor.Kind == LispReactorKind.Object
                    && what != "appended"
                    && reactor.Owns(handle)
                    && reactor.TryGetReaction("vlr-" + what, out name, out callback)
                )
                {
                    notifier = notifier ?? ReactorObject(handle);
                    calls.Add(new LispReactionCall(reactor, name, callback, new object[] { notifier, reactor, null }));
                }
            }
        }
    }
}
