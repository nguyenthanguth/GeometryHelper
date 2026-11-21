using System;
using System.Collections.Generic;
using System.Threading;

namespace GeometryHelper.Cad.Lisp
{
    /// <summary>
    /// What every interpreter of the process shares, as AutoCAD's session shares it among its documents: the blackboard of
    /// <c>vl-bb-set</c> and <c>vl-bb-ref</c>, and the symbols and files that <c>vl-propagate</c> and <c>vl-load-all</c> hand to
    /// the other interpreters. An interpreter is never touched from another thread: what is propagated waits here, and each
    /// interpreter takes it, in the order it was made, at the start of its next top-level evaluation (a new interpreter at its
    /// first).
    /// </summary>
    internal static class LispApplication
    {
        private sealed class Propagation
        {
            internal long Sequence;
            internal long SourceId;
            internal string Symbol;
            internal object Value;
            internal bool Defined;
            internal string File;
        }

        private static readonly object s_lock = new object();
        private static readonly Dictionary<string, object> s_blackboard = new Dictionary<string, object>(
            StringComparer.Ordinal
        );
        private static readonly List<Propagation> s_propagations = new List<Propagation>();
        private static long s_sequence;
        private static long s_nextId;

        /// <summary>Gives a new interpreter its number, which tells it from the others, and nothing yet applied.</summary>
        /// <param name="lisp">The interpreter, called by its constructor.</param>
        internal static void Register(LispInterpreter lisp)
        {
            lisp.ApplicationId = Interlocked.Increment(ref s_nextId);
        }

        /// <summary>Sets a name of the blackboard.</summary>
        /// <param name="name">The name, in upper case.</param>
        /// <param name="value">The value as the setting interpreter holds it.</param>
        internal static void SetBlackboard(string name, object value)
        {
            lock (s_lock)
            {
                s_blackboard[name] = value;
            }
        }

        /// <summary>Gets a name of the blackboard.</summary>
        /// <param name="name">The name, in upper case.</param>
        /// <returns>The value as the setting interpreter held it, to be copied by the reader; null for a name never set.</returns>
        internal static object GetBlackboard(string name)
        {
            lock (s_lock)
            {
                return s_blackboard.TryGetValue(name, out object value) ? value : null;
            }
        }

        /// <summary>Hands a symbol and its value to every other interpreter, now open or made later.</summary>
        /// <param name="source">The interpreter that has it.</param>
        /// <param name="symbol">The name, in upper case.</param>
        /// <param name="value">The value in the source.</param>
        /// <param name="defined">True when the value is a function made by <c>defun</c>, so that <c>c:name</c> stays a command.</param>
        internal static void Propagate(LispInterpreter source, string symbol, object value, bool defined)
        {
            lock (s_lock)
            {
                s_propagations.RemoveAll(p =>
                    p.Symbol != null && string.Equals(p.Symbol, symbol, StringComparison.Ordinal)
                );
                s_propagations.Add(
                    new Propagation
                    {
                        Sequence = ++s_sequence,
                        SourceId = source.ApplicationId,
                        Symbol = symbol,
                        Value = value,
                        Defined = defined,
                    }
                );
            }
        }

        /// <summary>Hands a file to every other interpreter to load, now open or made later.</summary>
        /// <param name="source">The interpreter that loaded it.</param>
        /// <param name="path">The full path of the file.</param>
        internal static void PropagateLoad(LispInterpreter source, string path)
        {
            lock (s_lock)
            {
                s_propagations.RemoveAll(p =>
                    p.File != null && string.Equals(p.File, path, StringComparison.OrdinalIgnoreCase)
                );
                s_propagations.Add(
                    new Propagation
                    {
                        Sequence = ++s_sequence,
                        SourceId = source.ApplicationId,
                        File = path,
                    }
                );
            }
        }

        /// <summary>
        /// Puts into an interpreter what the others propagated since it last looked, oldest first: the symbols with their values
        /// copied, the files loaded. A file that fails to load is skipped, as nobody is there to read the error. Called on the
        /// worker thread at the start of every top-level evaluation.
        /// </summary>
        /// <param name="lisp">The interpreter.</param>
        internal static void ApplyPending(LispInterpreter lisp)
        {
            List<Propagation> due = null;

            lock (s_lock)
            {
                if (lisp.AppliedSequence == s_sequence)
                {
                    return;
                }

                foreach (Propagation p in s_propagations)
                {
                    if (p.Sequence > lisp.AppliedSequence && p.SourceId != lisp.ApplicationId)
                    {
                        (due ?? (due = new List<Propagation>())).Add(p);
                    }
                }

                lisp.AppliedSequence = s_sequence;
            }

            if (due == null)
            {
                return;
            }

            foreach (Propagation p in due)
            {
                if (p.File != null)
                {
                    lisp.ApplyLoaded(p.File);
                }
                else
                {
                    lisp.ApplyPropagated(p.Symbol, p.Value, p.Defined);
                }
            }
        }

        /// <summary>Forgets the blackboard and everything propagated; for tests, which call it when they are done.</summary>
        internal static void Reset()
        {
            lock (s_lock)
            {
                s_blackboard.Clear();
                s_propagations.Clear();
            }
        }
    }
}
