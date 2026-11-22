namespace GeometryHelper.Cad.Lisp
{
    /// <summary>
    /// <c>command</c>, <c>vl-cmdf</c> and the symbol <c>PAUSE</c>: the arguments go, one after the other, to the command
    /// running or start one by name, as typed at the command line. The session does the feeding on the owner thread
    /// (<see cref="CadCommandSession.FeedLispArgument"/>); a <c>PAUSE</c> hands the turn to the user until the command
    /// asks its next prompt or ends.
    /// </summary>
    internal static partial class LispDrawing
    {
        // PAUSE is the symbol whose value is a backslash, which AutoCAD takes as the user's answer to the prompt.
        private const string Pause = "\\";

        static partial void RegisterCommand(LispInterpreter lisp, CadCommandSession session)
        {
            lisp.SetValue("PAUSE", Pause);
            lisp.Define("COMMAND", 0, -1, a => Command(lisp, session, a, vl: false));
            lisp.Define("VL-CMDF", 0, -1, a => Command(lisp, session, a, vl: true));
            lisp.Define("COMMAND-S", 0, -1, a => CommandStack(lisp, session, a));
        }

        /// <summary>
        /// <c>(command [arg ...])</c> and <c>(vl-cmdf [arg ...])</c>. A prompt that refuses an argument writes its words; then
        /// <c>command</c> fails with <c>Function cancelled</c>, the command left at its prompt, and <c>vl-cmdf</c> passes
        /// nothing more and returns T. <c>command</c> returns nil, <c>vl-cmdf</c> T; <c>(command)</c> cancels the command
        /// running. An argument that is no string, number, point, entity name, selection set or nil is
        /// <c>bad argument value: AutoCAD command: </c> and what it is.
        /// </summary>
        private static object Command(LispInterpreter lisp, CadCommandSession session, object[] a, bool vl)
        {
            // A dialog on the screen takes no commands: the evaluation ends (D343).
            if (lisp.RunningDialogs > 0)
            {
                throw lisp.EndEvaluation("AutoCAD rejected function");
            }

            // *error* may call command only where the routine said so before the error came.
            lisp.CheckCommandAllowed();

            if (a.Length == 0)
            {
                lisp.OnOwner(() =>
                {
                    session.CancelLispCommand();
                    return null;
                });
                return vl ? lisp.Bool(true) : null;
            }

            bool refused = false;

            foreach (object argument in a)
            {
                if (argument is string text && text == Pause)
                {
                    // Nothing is waited at where no command runs.
                    if ((bool)lisp.OnOwner(() => session.CanLispPause()))
                    {
                        lisp.Ask(new LispAsk { Kind = LispAskKind.Pause });
                    }

                    continue;
                }

                object arg = argument;
                var fed = (LispFeed)lisp.OnOwner(() => session.FeedLispArgument(arg));

                if (fed == LispFeed.Bad)
                {
                    throw new LispException("bad argument value: AutoCAD command: " + LispInterpreter.Print(arg, true));
                }

                if (fed == LispFeed.Refused)
                {
                    refused = true;
                    break;
                }
            }

            if (refused && !vl)
            {
                throw new LispException("Function cancelled");
            }

            if (!refused)
            {
                lisp.OnOwner(() =>
                {
                    session.EndLispCommand();
                    return null;
                });
            }

            return vl ? lisp.Bool(true) : null;
        }

        /// <summary>
        /// <c>(command-s [arg ...])</c>, the form of <c>command</c> that never waits half way: the first argument names the
        /// command and the others answer its prompts, one after the other. A prompt that refuses one writes its words and takes
        /// the next at the same prompt; what the command leaves unanswered is waited for until it ends, and the arguments it did
        /// not take, once it has ended, are dropped. The value is nil. It is allowed in <c>*error*</c> with no
        /// <c>*push-error-using-command*</c>. An argument that is nil, or a list that is no point, writes its words and fails
        /// with <c>Unknown (command-s) failure.</c> as does a first argument that names no command; a symbol or a function is
        /// <c>Invalid data type for (command-s).</c> (AutoCAD 2021, probes g6 to g6d).
        /// </summary>
        private static object CommandStack(LispInterpreter lisp, CadCommandSession session, object[] a)
        {
            if (lisp.RunningDialogs > 0)
            {
                throw lisp.EndEvaluation("AutoCAD rejected function");
            }

            if (a.Length == 0)
            {
                lisp.OnOwner(() =>
                {
                    session.CancelLispCommand();
                    return null;
                });
                return null;
            }

            foreach (object argument in a)
            {
                string words = null;

                switch (argument)
                {
                    case null:
                        words = "Application ERROR: Invalid type sent as command input";
                        break;
                    case string _:
                    case int _:
                    case double _:
                    case LispEntityName _:
                    case LispSelectionSet _:
                        break;
                    case LispCons _ when PointOf(argument) != null:
                        break;
                    case LispCons _:
                        words = "Application ERROR: Invalid entity/point list.";
                        break;
                    default:
                        throw new LispException("Invalid data type for (command-s).");
                }

                if (words != null)
                {
                    lisp.WriteFreshLine();
                    lisp.Write(words + "\n");
                    throw new LispException("Unknown (command-s) failure.");
                }
            }

            if ((bool)lisp.OnOwner(() => session.FeedLispStack(a)))
            {
                lisp.Ask(new LispAsk { Kind = LispAskKind.Pause, UntilEnd = true });
            }

            lisp.OnOwner(() =>
            {
                session.EndLispStack();
                return null;
            });
            return null;
        }
    }
}
