using System;
using System.IO;
using System.Linq;
using GeometryHelper.Cad.Lisp;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The reactors of Visual LISP as AutoCAD 2021's accoreconsole did them (probe m1 of the L8 round): the nineteen
    /// constructors and what they print, the accessors, the order reactors are listed and called in, and the events the presenter
    /// raises: command, LISP, system variable, drawing, object and database. A callback of the tests writes
    /// <c>(event . parameters)</c> to the list <c>log</c>, which a test reads back oldest first.
    /// </summary>
    public sealed class LispReactorTests : IDisposable
    {
        // Ends the routine a test left waiting, so no worker thread outlives its test.
        public void Dispose() => WaitingRoutines.EndAll();

        private const string ObjectCallback =
            "(defun cbo (n r a) (setq log (cons (cons (vlr-current-reaction-name) a) log)))";

        private const string LogCallback =
            "(defun cb (r a) (setq log (cons (cons (vlr-current-reaction-name) a) log)))";

        // A session whose callback `cb` logs what it is given.
        private static CadCommandSession Logging(CadDocument document = null)
        {
            CadCommandSession session = Session(document ?? Plan());
            Run(session, "(setq log nil)", LogCallback, ObjectCallback);
            return session;
        }

        // What the evaluation of a form gives, as prin1 writes it; the evaluation does not go through the command line.
        private static string Eval(CadCommandSession session, string form)
        {
            LispResult result = session.Lisp.Run(form);
            Assert.False(result.Failed, result.Output);
            return LispInterpreter.Print(result.Value, true);
        }

        // Evaluates forms for set-up without the command line: they raise no event of their own.
        private static void Quiet(CadCommandSession session, params string[] forms)
        {
            foreach (string form in forms)
            {
                LispResult result = session.Lisp.Run(form);
                Assert.False(result.Failed, result.Output);
            }
        }

        // The log, oldest first.
        private static string Log(CadCommandSession session)
        {
            var items = LispInterpreter.ItemsOf(session.Lisp.GetValue("LOG"));
            items.Reverse();
            return LispInterpreter.Print(LispInterpreter.ListOf(items), true);
        }

        private static string[] Lines(CadCommandSession session) => session.History.Select(l => l.TrimEnd()).ToArray();

        #region Constructors and accessors

        [Fact]
        public void EveryConstructor_MakesAnAddedReactorThatPrintsAsItsType()
        {
            CadCommandSession session = Session(Blank());

            foreach (
                string kind in new[]
                {
                    "Command",
                    "Editor",
                    "AcDb",
                    "DocManager",
                    "Lisp",
                    "DXF",
                    "DWG",
                    "Insert",
                    "Wblock",
                    "SysVar",
                    "DeepClone",
                    "XREF",
                    "Undo",
                    "Window",
                    "Toolbar",
                    "Mouse",
                    "Miscellaneous",
                    "Linker",
                    "Object",
                }
            )
            {
                string constructor = "vlr-" + kind + "-reactor";
                Assert.Equal(
                    "\"#<VLR-" + kind + "-Reactor>\"",
                    Eval(session, "(vl-prin1-to-string (" + constructor + " nil nil))")
                );
                Assert.Equal("T", Eval(session, "(vlr-added-p (" + constructor + " nil nil))"));
            }
        }

        [Fact]
        public void TheTypeOfAReactor_IsTheSymbolOfItsNameAndVlrTypeItsKeyword()
        {
            CadCommandSession session = Session(Blank());

            Assert.Equal("VLR-Command-Reactor", Eval(session, "(type (vlr-command-reactor nil nil))"));
            Assert.Equal(":VLR-Command-Reactor", Eval(session, "(vlr-type (vlr-command-reactor nil nil))"));
        }

        [Fact]
        public void VlrTypes_ListsTheNineteenKindsInAutoCadsOrder()
        {
            CadCommandSession session = Session(Blank());

            Assert.Equal(
                "(:VLR-Linker-Reactor :VLR-Editor-Reactor :VLR-AcDb-Reactor :VLR-DocManager-Reactor :VLR-Command-Reactor "
                    + ":VLR-Lisp-Reactor :VLR-DXF-Reactor :VLR-DWG-Reactor :VLR-Insert-Reactor :VLR-Wblock-Reactor "
                    + ":VLR-SysVar-Reactor :VLR-DeepClone-Reactor :VLR-XREF-Reactor :VLR-Undo-Reactor :VLR-Window-Reactor "
                    + ":VLR-Toolbar-Reactor :VLR-Mouse-Reactor :VLR-Miscellaneous-Reactor :VLR-Object-Reactor)",
                Eval(session, "(vlr-types)")
            );
        }

        [Fact]
        public void AConstructorWithNoArguments_IsAllowedAndOneWithTooManyIsAnError()
        {
            CadCommandSession session = Session(Blank());

            Assert.Equal("\"#<VLR-Command-Reactor>\"", Eval(session, "(vl-prin1-to-string (vlr-command-reactor))"));
            Assert.Equal(
                "\"too many arguments\"",
                Eval(session, "(vl-catch-all-error-message (vl-catch-all-apply 'vlr-command-reactor '(nil nil nil)))")
            );
        }

        [Fact]
        public void VlrReactors_ListsTheGroupsNewestFirstWithTheNewestReactorOfAGroupFirst()
        {
            CadCommandSession session = Session(Blank());
            Run(
                session,
                "(setq c1 (vlr-command-reactor \"first\" nil))",
                "(setq c2 (vlr-command-reactor \"second\" nil))",
                "(setq l1 (vlr-lisp-reactor \"lisp\" nil))",
                "(setq c3 (vlr-command-reactor \"third\" nil))"
            );

            Assert.DoesNotContain(Lines(session), l => l.StartsWith("; error", StringComparison.Ordinal));
            Assert.Equal(
                "((:VLR-Command-Reactor \"third\" \"second\" \"first\") (:VLR-Lisp-Reactor \"lisp\"))",
                Eval(session, "(mapcar '(lambda (g) (cons (car g) (mapcar 'vlr-data (cdr g)))) (vlr-reactors))")
            );
            Assert.Equal(
                "((:VLR-Command-Reactor \"third\" \"second\" \"first\"))",
                Eval(
                    session,
                    "(mapcar '(lambda (g) (cons (car g) (mapcar 'vlr-data (cdr g)))) (vlr-reactors :vlr-command-reactor))"
                )
            );
        }

        [Fact]
        public void VlrReactorsOfAKindWithNoReactorAndOfAnUnknownKind_AreNilAndAnError()
        {
            CadCommandSession session = Session(Blank());

            Assert.Equal("nil", Eval(session, "(vlr-reactors)"));
            Assert.Equal("nil", Eval(session, "(vlr-reactors :vlr-command-reactor)"));
            Assert.Equal(
                "\"unknown reactor type: nil\"",
                Eval(session, "(vl-catch-all-error-message (vl-catch-all-apply 'vlr-reactors '(nil)))")
            );
            Assert.Equal(
                "\"unknown reactor type: \\\"x\\\"\"",
                Eval(session, "(vl-catch-all-error-message (vl-catch-all-apply 'vlr-reactors '(\"x\")))")
            );
        }

        [Fact]
        public void TheReactionsOfAReactor_ReadBackTheLastGivenFirstAsPairsOfAKeywordAndAnUpperCaseCallback()
        {
            CadCommandSession session = Session(Blank());
            Run(
                session,
                "(setq r (vlr-command-reactor nil '((:vlr-commandWillStart . cb) (:vlr-commandEnded . \"cb2\"))))"
            );

            Assert.Equal(
                "((:VLR-commandEnded . CB2) (:VLR-commandWillStart . CB))",
                Eval(session, "(vlr-reactions r)")
            );
            Assert.Equal("\"CB3\"", Eval(session, "(vlr-reaction-set r :vlr-commandCancelled 'cb3)"));
            Assert.Equal(
                "((:VLR-commandCancelled . CB3) (:VLR-commandEnded . CB2) (:VLR-commandWillStart . CB))",
                Eval(session, "(vlr-reactions r)")
            );
            Assert.Equal("\"CB4\"", Eval(session, "(vlr-reaction-set r :vlr-commandEnded 'cb4)"));
            Assert.Equal(
                "((:VLR-commandCancelled . CB3) (:VLR-commandEnded . CB4) (:VLR-commandWillStart . CB))",
                Eval(session, "(vlr-reactions r)")
            );
        }

        [Fact]
        public void ANameGivenTwice_KeepsTheLastCallback()
        {
            CadCommandSession session = Session(Blank());
            Run(
                session,
                "(setq r (vlr-command-reactor nil '((:vlr-commandWillStart . cb) (:vlr-commandWillStart . cb2))))"
            );

            Assert.Equal("((:VLR-commandWillStart . CB2))", Eval(session, "(vlr-reactions r)"));
        }

        [Theory]
        [InlineData("(vlr-command-reactor nil '(a b))", "bad argument type: consp A")]
        [InlineData("(vlr-command-reactor nil 5)", "bad argument type: consp 5")]
        [InlineData(
            "(vlr-command-reactor nil '((:vlr-commandWillStart . 5)))",
            "bad argument type: (or stringp symbolp): 5"
        )]
        [InlineData(
            "(vlr-command-reactor nil '((:vlr-commandWillStart)))",
            "bad argument type: (or stringp symbolp): nil"
        )]
        [InlineData("(vlr-command-reactor nil '((:vlr-nosuch . cb)))", "invalid reaction name: :VLR-NOSUCH")]
        [InlineData("(vlr-command-reactor nil '((:vlr-modified . cb)))", "invalid reaction name: :VLR-modified")]
        [InlineData(
            "(vlr-command-reactor nil '((\"vlr-commandWillStart\" . cb)))",
            "invalid reaction name: \"vlr-commandWillStart\""
        )]
        [InlineData(
            "(vlr-command-reactor nil '((vlr-commandWillStart . cb)))",
            "invalid reaction name: VLR-COMMANDWILLSTART"
        )]
        [InlineData(
            "(vlr-object-reactor nil nil '((:vlr-commandWillStart . cb)))",
            "invalid reaction name: :VLR-commandWillStart"
        )]
        [InlineData("(vlr-object-reactor 5 nil nil)", "bad argument type: consp 5")]
        [InlineData(
            "(vlr-object-reactor (list 1) nil nil)",
            "parameter is not a valid dispatch interface (IDispatch) argument: 1"
        )]
        [InlineData(
            "(vlr-command-reactor nil '((:vlr-lispWillStart . cb)))",
            "invalid reaction name: :VLR-lispWillStart"
        )]
        [InlineData("(vlr-add 5)", "bad argument type: VLReactor: 5")]
        [InlineData(
            "(vlr-owners (vlr-command-reactor nil nil))",
            "bad argument type: Object reactor: #<VLR-Command-Reactor>"
        )]
        [InlineData(
            "(vlr-reaction-set (vlr-command-reactor nil nil) :vlr-commandEnded nil)",
            "invalid reaction function name: nil"
        )]
        [InlineData(
            "(vlr-reaction-set (vlr-command-reactor nil nil) :vlr-modified 'cb)",
            "invalid reaction name: :VLR-modified"
        )]
        [InlineData("(vlr-reaction-names 5)", "bad argument type: must be reactor object or reactor type keyword: 5")]
        [InlineData("(vlr-set-notification (vlr-command-reactor nil nil) 'all)", "Incorrect notification value: ALL")]
        public void AnArgumentTheFunctionRefuses_GivesAutoCadsMessage(string form, string message)
        {
            CadCommandSession session = Session(Blank());
            LispResult result = session.Lisp.Run(
                "(vl-catch-all-error-message (vl-catch-all-apply '(lambda () " + form + ") nil))"
            );

            Assert.False(result.Failed, result.Output);
            Assert.Equal(message, Assert.IsType<string>(result.Value));
        }

        [Fact]
        public void VlrReactionNamesOfAKind_ListTheNamesAutoCadHas()
        {
            CadCommandSession session = Session(Blank());

            Assert.Equal(
                "(:VLR-unknownCommand :VLR-commandWillStart :VLR-commandEnded :VLR-commandCancelled :VLR-commandFailed)",
                Eval(session, "(vlr-reaction-names :vlr-command-reactor)")
            );
            Assert.Equal(
                "(:VLR-sysVarWillChange :VLR-sysVarChanged)",
                Eval(session, "(vlr-reaction-names (vlr-sysvar-reactor nil nil))")
            );
        }

        [Fact]
        public void TheDatumOfAReactor_IsKeptAsGivenAndVlrDataSetGivesTheNewOne()
        {
            CadCommandSession session = Session(Blank());
            Run(session, "(setq r (vlr-command-reactor '(1 \"a\") nil))");

            Assert.Equal("(1 \"a\")", Eval(session, "(vlr-data r)"));
            Assert.Equal("5", Eval(session, "(vlr-data-set r 5)"));
            Assert.Equal("5", Eval(session, "(vlr-data r)"));
        }

        [Fact]
        public void VlrRemoveAndVlrAdd_GiveTheReactorWhenTheyChangeItAndNilWhenTheyDoNot()
        {
            CadCommandSession session = Session(Blank());
            Run(session, "(setq r (vlr-command-reactor nil nil))");

            Assert.Equal("nil", Eval(session, "(vlr-add r)"));
            Assert.Equal("\"#<VLR-Command-Reactor>\"", Eval(session, "(vl-prin1-to-string (vlr-remove r))"));
            Assert.Equal("nil", Eval(session, "(vlr-remove r)"));
            Assert.Equal("nil", Eval(session, "(vlr-added-p r)"));
            Assert.Equal("nil", Eval(session, "(vlr-reactors)"));
            Assert.Equal("\"#<VLR-Command-Reactor>\"", Eval(session, "(vl-prin1-to-string (vlr-add r))"));
            Assert.Equal("T", Eval(session, "(vlr-added-p r)"));
        }

        [Fact]
        public void VlrRemoveAll_GivesWhatItRemovedInTheShapeOfVlrReactorsAndNilWhenThereIsNothing()
        {
            CadCommandSession session = Session(Blank());
            Run(session, "(vlr-command-reactor \"c\" nil)", "(vlr-lisp-reactor \"l\" nil)");

            Assert.Equal(
                "((:VLR-Command-Reactor \"c\"))",
                Eval(
                    session,
                    "(mapcar '(lambda (g) (cons (car g) (mapcar 'vlr-data (cdr g)))) (vlr-remove-all :vlr-command-reactor))"
                )
            );
            Assert.Equal("nil", Eval(session, "(vlr-remove-all :vlr-command-reactor)"));
            Assert.Equal(
                "((:VLR-Lisp-Reactor \"l\"))",
                Eval(session, "(mapcar '(lambda (g) (cons (car g) (mapcar 'vlr-data (cdr g)))) (vlr-remove-all))")
            );
            Assert.Equal("nil", Eval(session, "(vlr-reactors)"));
        }

        [Fact]
        public void TheOwnersOfAnObjectReactor_AreAddedAndRemovedAndNotAddedTwice()
        {
            CadCommandSession session = Session(Plan());
            Run(
                session,
                "(vl-load-com)",
                "(setq o (vlax-ename->vla-object (entlast)))",
                "(setq r (vlr-object-reactor nil nil nil))"
            );

            Assert.Equal("nil", Eval(session, "(vlr-owners r)"));
            Run(session, "(vlr-owner-add r o)", "(vlr-owner-add r (vlax-ename->vla-object (entlast)))");
            Assert.Equal("1", Eval(session, "(length (vlr-owners r))"));
            Run(session, "(vlr-owner-remove r o)");
            Assert.Equal("nil", Eval(session, "(vlr-owners r)"));
        }

        [Fact]
        public void TheMarkOfAPersistentReactor_IsKeptForTheSessionAndReleased()
        {
            CadCommandSession session = Session(Blank());
            Run(session, "(setq r (vlr-command-reactor nil nil))");

            Assert.Equal("nil", Eval(session, "(vlr-pers-p r)"));
            Assert.Equal("nil", Eval(session, "(vlr-pers-list)"));
            Run(session, "(vlr-pers r)");
            Assert.Equal("T", Eval(session, "(eq r (vlr-pers-p r))"));
            Assert.Equal("1", Eval(session, "(length (vlr-pers-list))"));
            Run(session, "(vlr-pers-release r)");
            Assert.Equal("nil", Eval(session, "(vlr-pers-p r)"));
            Assert.Equal("\"VL-REACTORS\"", Eval(session, "(vlr-pers-dictname)"));
        }

        [Fact]
        public void TheNotificationOfAReactor_IsActiveDocumentOnlyUntilSet()
        {
            CadCommandSession session = Session(Blank());
            Run(session, "(setq r (vlr-command-reactor nil nil))");

            Assert.Equal("active-document-only", Eval(session, "(vlr-notification r)"));
            Run(session, "(vlr-set-notification r 'all-documents)");
            Assert.Equal("ALL-DOCUMENTS", Eval(session, "(vlr-notification r)"));
        }

        [Fact]
        public void VlLoadReactors_GivesTheEmptySymbolAndVlrTraceReactionWritesTheReactionAndItsArguments()
        {
            CadCommandSession session = Session(Blank());

            Assert.Equal("", Eval(session, "(vl-load-reactors)"));
            Assert.Equal("nil", Eval(session, "(vlr-beep-reaction 1 2)"));

            LispResult result = session.Lisp.Run("(vlr-trace-reaction 1 2)");
            Assert.Contains("; Reaction: nil; argument list: (1 2)", result.Output);
        }

        [Fact]
        public void VlaxQueueexpr_TakesAStringAndGivesNil()
        {
            CadCommandSession session = Session(Blank());

            Assert.Equal("nil", Eval(session, "(vlax-queueexpr \"(princ 1)\")"));
            Assert.Equal(
                "\"bad argument type: stringp (PRINC 1)\"",
                Eval(session, "(vl-catch-all-error-message (vl-catch-all-apply 'vlax-queueexpr (list '(princ 1))))")
            );
        }

        #endregion

        #region Command events

        [Fact]
        public void ATypedLine_RaisesWillStartBeforeItsFirstPromptAndEndedBeforeTheNextCommandPrompt()
        {
            CadCommandSession session = Logging();
            Run(session, "(vlr-command-reactor nil '((:vlr-commandWillStart . cb) (:vlr-commandEnded . cb)))");

            Run(session, "LINE", P(0, 0), P(10, 0), Enter);

            Assert.Equal("((:VLR-commandWillStart \"LINE\") (:VLR-commandEnded \"LINE\"))", Log(session));
        }

        [Fact]
        public void ACommandCancelledByEsc_RaisesCancelledAndNotEnded()
        {
            CadCommandSession session = Logging();
            Run(session, "(vlr-command-reactor nil '((:vlr-commandEnded . cb) (:vlr-commandCancelled . cb)))");

            Run(session, "LINE", P(0, 0), Esc);

            Assert.Equal("((:VLR-commandCancelled \"LINE\"))", Log(session));
        }

        [Fact]
        public void AnAliasTyped_IsReportedByTheCommandsName()
        {
            CadCommandSession session = Logging();
            Run(session, "(vlr-command-reactor nil '((:vlr-commandWillStart . cb)))");

            Run(session, "L", P(0, 0), Esc);

            Assert.Equal("((:VLR-commandWillStart \"LINE\"))", Log(session));
        }

        [Fact]
        public void AWordThatIsNoCommand_RaisesUnknownCommandBeforeTheMessageForCommandAndEditorReactors()
        {
            CadCommandSession session = Logging();
            Run(
                session,
                "(vlr-command-reactor nil '((:vlr-unknownCommand . cb)))",
                "(vlr-editor-reactor nil '((:vlr-unknownCommand . cb)))"
            );

            session.Feed("NOSUCHCMD");

            Assert.Equal("((:VLR-unknownCommand \"NOSUCHCMD\") (:VLR-unknownCommand \"NOSUCHCMD\"))", Log(session));
            Assert.Contains("Unknown command \"NOSUCHCMD\".  Press F1 for help.", Lines(session));
        }

        [Fact]
        public void ACommandOfARoutine_RaisesItsEventsInOrderWithTheRoutinesOwnForms()
        {
            CadCommandSession session = Logging();
            Run(
                session,
                "(vlr-command-reactor nil '((:vlr-commandWillStart . cb) (:vlr-commandEnded . cb)))",
                "(defun c:t1 () (setq log (cons \"A\" log)) (command \"_.LINE\" \"0,0\" \"10,0\" \"\") (setq log (cons \"B\" log)) (princ))"
            );

            session.Feed("T1");

            Assert.Equal("(\"A\" (:VLR-commandWillStart \"LINE\") (:VLR-commandEnded \"LINE\") \"B\")", Log(session));
        }

        [Fact]
        public void ATransparentZoomInsideALine_RaisesItsOwnWillStartAndEndedAndNotTheLinesAgain()
        {
            CadCommandSession session = Logging();
            Run(session, "(vlr-command-reactor nil '((:vlr-commandWillStart . cb) (:vlr-commandEnded . cb)))");

            Run(session, "LINE", P(0, 0), "'ZOOM", "E", Esc);

            Assert.Equal(
                "((:VLR-commandWillStart \"LINE\") (:VLR-commandWillStart \"ZOOM\") (:VLR-commandEnded \"ZOOM\"))",
                Log(session)
            );
        }

        [Fact]
        public void AnEditorReactor_HearsTheCommandEventsAfterTheCommandReactorMadeBeforeIt()
        {
            CadCommandSession session = Logging();
            Run(
                session,
                "(defun cbc (r a) (setq log (cons \"command\" log)))",
                "(defun cbe (r a) (setq log (cons \"editor\" log)))",
                "(vlr-command-reactor nil '((:vlr-commandWillStart . cbc)))",
                "(vlr-editor-reactor nil '((:vlr-commandWillStart . cbe)))"
            );

            Run(session, "LINE", Esc);

            Assert.Equal("(\"command\" \"editor\")", Log(session));
        }

        [Fact]
        public void TheReactorsOfAKind_AreCalledOldestFirstAndOneAddedAgainGoesLast()
        {
            CadCommandSession session = Logging();
            Run(
                session,
                "(defun cba (r a) (setq log (cons (vlr-data r) log)))",
                "(setq r1 (vlr-command-reactor \"first\" '((:vlr-commandWillStart . cba))))",
                "(setq r2 (vlr-command-reactor \"second\" '((:vlr-commandWillStart . cba))))",
                "(setq r3 (vlr-command-reactor \"third\" '((:vlr-commandWillStart . cba))))"
            );

            Run(session, "LINE", Esc);
            Run(session, "(vlr-remove r2)", "(vlr-add r2)");
            Run(session, "LINE", Esc);

            Assert.Equal("(\"first\" \"second\" \"third\" \"first\" \"third\" \"second\")", Log(session));
        }

        [Fact]
        public void AReactorRemoved_StopsAnsweringAndAddedAgainStartsAgain()
        {
            CadCommandSession session = Logging();
            Run(session, "(setq r (vlr-command-reactor nil '((:vlr-commandWillStart . cb))))");

            Run(session, "(vlr-remove r)", "LINE", Esc);
            Assert.Equal("nil", Log(session));

            Run(session, "(vlr-add r)", "LINE", Esc);
            Assert.Equal("((:VLR-commandWillStart \"LINE\"))", Log(session));
        }

        [Fact]
        public void AReactorThatRemovesItselfInItsCallback_FinishesTheCallbackAndTheCommandGoesOn()
        {
            CadCommandSession session = Logging();
            Run(
                session,
                "(defun cbr (r a) (vlr-remove r) (setq log (cons \"removed\" log)))",
                "(setq r (vlr-command-reactor nil '((:vlr-commandWillStart . cbr))))"
            );

            Run(session, "LINE", P(0, 0), Esc, "LINE", Esc);

            Assert.Equal("(\"removed\")", Log(session));
            Assert.Equal("nil", Eval(session, "(vlr-added-p r)"));
        }

        [Fact]
        public void TheOutputOfACallbackThatRunsAtOnce_IsWrittenBeforeTheCommandsFirstPromptWithNoEcho()
        {
            CadCommandSession session = Logging();
            Run(
                session,
                "(defun say (r a) (princ \"hello\"))",
                "(vlr-command-reactor nil '((:vlr-commandWillStart . say)))"
            );

            Run(session, "LINE", Esc);

            string[] lines = Lines(session);
            int hello = Array.IndexOf(lines, "hello");
            Assert.True(hello > 0, string.Join("|", lines));
            Assert.Equal("Command: LINE", lines[hello - 1]);
            Assert.StartsWith("Specify first point:", lines[hello + 1], StringComparison.Ordinal);
        }

        [Fact]
        public void EventsThatComeWhileARoutineWaitsAtAGetFunction_AreAnsweredBeforeItGoesOn()
        {
            CadCommandSession session = Logging();
            Run(
                session,
                "(defun c:ask () (setq log (cons \"before\" log)) (getpoint \"P: \") (setq log (cons \"after\" log)) (princ))",
                "(vlr-command-reactor nil '((:vlr-commandWillStart . cb) (:vlr-commandEnded . cb)))"
            );

            Run(session, "ASK", "'ZOOM", "E");
            Assert.Equal("(\"before\")", Log(session));

            Run(session, P(5, 5));

            Assert.Equal(
                "(\"before\" (:VLR-commandWillStart \"ZOOM\") (:VLR-commandEnded \"ZOOM\") \"after\")",
                Log(session)
            );
        }

        [Fact]
        public void ACommandWillStartThatSetsTheCurrentLayer_PutsWhatTheCommandMakesOnThatLayer()
        {
            CadCommandSession session = Session(Plan());
            Run(
                session,
                "(entmake '((0 . \"LAYER\") (100 . \"AcDbSymbolTableRecord\") (100 . \"AcDbLayerTableRecord\") (2 . \"L2\") (70 . 0) (62 . 1) (6 . \"Continuous\")))",
                "(defun setl (r a) (setvar \"CLAYER\" \"L2\"))",
                "(vlr-command-reactor nil '((:vlr-commandWillStart . setl)))"
            );

            Run(session, "TEXT", P(10, 10), "2.5", "0", "ABC", Enter);

            DxfDBText text = All<DxfDBText>(session.Document.ModelSpace).Last();
            Assert.Equal("L2", text.Layer);
        }

        #endregion

        #region Callbacks

        [Fact]
        public void AnErrorInACallback_IsWrittenAfterItsOutputAndTheCommandAndTheReactorGoOn()
        {
            CadCommandSession session = Logging();
            Run(
                session,
                "(defun bad (r a) (/ 1 0))",
                "(setq r (vlr-command-reactor nil '((:vlr-commandWillStart . bad))))"
            );

            int before = All<DxfLine>(session.Document.ModelSpace).Count;

            Run(session, "LINE", P(0, 0), P(10, 0), Enter);

            Assert.Contains("; error: divide by zero", Lines(session));
            Assert.Equal(CadCommandOutcome.Completed, session.LastOutcome);
            Assert.Equal(before + 1, All<DxfLine>(session.Document.ModelSpace).Count);
            Assert.Equal("T", Eval(session, "(vlr-added-p r)"));
        }

        [Fact]
        public void AnErrorInACallback_CallsTheRoutinesErrorFunctionWhereItHasOne()
        {
            CadCommandSession session = Logging();
            Run(
                session,
                "(defun *error* (m) (setq log (cons (list \"error\" m) log)) (princ))",
                "(defun bad (r a) (/ 1 0))",
                "(vlr-command-reactor nil '((:vlr-commandWillStart . bad)))"
            );

            Run(session, "LINE", Esc);

            Assert.Equal("((\"error\" \"divide by zero\"))", Log(session));
            Assert.DoesNotContain("; error: divide by zero", Lines(session));
        }

        [Theory]
        [InlineData("(defun bad () 1)", "; error: too many arguments")]
        [InlineData("(defun bad (a b c) 1)", "; error: too few arguments")]
        [InlineData("(defun other () 1)", "; error: no function definition: BAD")]
        public void ACallbackOfTheWrongShapeOrNotDefined_WritesAutoCadsError(string definition, string message)
        {
            CadCommandSession session = Logging();
            Run(session, definition, "(vlr-command-reactor nil '((:vlr-commandWillStart . bad)))");

            Run(session, "LINE", Esc);

            Assert.Contains(message, Lines(session));
        }

        [Fact]
        public void CommandInACallback_IsRefusedAndTheCommandThatRaisedTheEventGoesOn()
        {
            CadCommandSession session = Logging();
            Run(
                session,
                "(defun pt (r a) (command \"_.POINT\" \"1,1\"))",
                "(vlr-command-reactor nil '((:vlr-commandEnded . pt)))"
            );

            Run(session, "LINE", P(0, 0), P(10, 0), Enter);

            Assert.Contains("; error: AutoCAD command rejected: \"_.POINT\"", Lines(session));
            Assert.Empty(All<DxfDBPoint>(session.Document.ModelSpace));
        }

        [Fact]
        public void CommandSInACallback_IsRefused()
        {
            CadCommandSession session = Logging();
            Run(
                session,
                "(defun pt (r a) (command-s \"_.POINT\" \"1,1\"))",
                "(vlr-command-reactor nil '((:vlr-commandEnded . pt)))"
            );

            Run(session, "LINE", P(0, 0), P(10, 0), Enter);

            Assert.Contains("; error: Unknown (command-s) failure.", Lines(session));
        }

        [Fact]
        public void EntmakeAndSetvarInACallback_Work()
        {
            CadCommandSession session = Logging();
            Run(
                session,
                "(defun mk (r a) (entmakex '((0 . \"POINT\") (10 5 5 0))) (setvar \"LTSCALE\" 3))",
                "(vlr-command-reactor nil '((:vlr-commandEnded . mk)))"
            );

            Run(session, "LINE", P(0, 0), P(10, 0), Enter);

            Assert.Single(All<DxfDBPoint>(session.Document.ModelSpace));
            Assert.Equal("3.0", Eval(session, "(getvar \"LTSCALE\")"));
        }

        [Fact]
        public void ACallbackThatRaisesItsOwnEventAgain_StopsAtAThousandCallbacksWithOneLine()
        {
            CadCommandSession session = Logging();
            Run(
                session,
                "(setq count 0)",
                "(defun again (r a) (setq count (1+ count)) (setvar \"LTSCALE\" (+ 1.0 (* count 0.001))))",
                "(vlr-sysvar-reactor nil '((:vlr-sysVarChanged . again)))"
            );

            Run(session, "(setvar \"LTSCALE\" 2)");

            Assert.Equal("1000", Eval(session, "count"));
            Assert.Single(Lines(session), l => l == "; error: reactor cascade stopped");
        }

        #endregion

        #region Lisp, system variable and drawing events

        [Fact]
        public void ATypedForm_RaisesLispWillStartWithItsTextAndLispEndedWithNothing()
        {
            CadCommandSession session = Logging();
            Quiet(session, "(vlr-lisp-reactor nil '((:vlr-lispWillStart . cb) (:vlr-lispEnded . cb)))");

            Run(session, "(+ 1 2)");

            Assert.Equal("((:VLR-lispWillStart \"(+ 1 2)\") (:VLR-lispEnded))", Log(session));
        }

        [Fact]
        public void ALineWithTwoForms_RaisesTheStartOnceForEachInOrder()
        {
            CadCommandSession session = Logging();
            Quiet(session, "(vlr-lisp-reactor nil '((:vlr-lispWillStart . cb)))");

            Run(session, "(setq a 1) (setq b 2)");

            Assert.Equal("((:VLR-lispWillStart \"(setq a 1)\") (:VLR-lispWillStart \"(setq b 2)\"))", Log(session));
        }

        [Fact]
        public void AFunctionTypedAsACommand_RaisesLispWillStartWithTheCallOfItsFunction()
        {
            CadCommandSession session = Logging();
            Quiet(session, "(defun c:t1 () (princ))", "(vlr-lisp-reactor nil '((:vlr-lispWillStart . cb)))");

            session.Feed("T1");

            Assert.Equal("((:VLR-lispWillStart \"(C:T1)\"))", Log(session));
        }

        [Fact]
        public void ALoad_RaisesLispWillStartOnceForTheLoadAndNotForTheFormsInTheFile()
        {
            string path = Path.Combine(Path.GetTempPath(), "reactor-load-" + Guid.NewGuid().ToString("N") + ".lsp");
            File.WriteAllText(path, "(setq x 1)\n(setq y 2)\n");

            try
            {
                CadCommandSession session = Logging();
                Quiet(session, "(vlr-lisp-reactor nil '((:vlr-lispWillStart . cb)))");

                session.LoadLisp(path);

                Assert.Equal(2, Log(session).Split(new[] { ":VLR-lispWillStart" }, StringSplitOptions.None).Length);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void AFormThatFails_StillRaisesLispEndedAndOnlyEscAtAGetFunctionRaisesLispCancelled()
        {
            CadCommandSession session = Logging();
            Quiet(session, "(vlr-lisp-reactor nil '((:vlr-lispEnded . cb) (:vlr-lispCancelled . cb)))");

            Run(session, "(/ 1 0)");
            Assert.Equal("((:VLR-lispEnded))", Log(session));

            Quiet(session, "(setq log nil)");
            Run(session, "(getstring \"Name: \")", Esc);
            Assert.Equal("((:VLR-lispCancelled) (:VLR-lispEnded))", Log(session));
        }

        [Fact]
        public void ASetvarOfARoutine_RaisesWillChangeAndChangedWithTheNameAndT()
        {
            CadCommandSession session = Logging();
            Run(session, "(vlr-sysvar-reactor nil '((:vlr-sysVarWillChange . cb) (:vlr-sysVarChanged . cb)))");

            Run(session, "(setvar \"LTSCALE\" 2)");

            Assert.Equal("((:VLR-sysVarWillChange \"LTSCALE\") (:VLR-sysVarChanged \"LTSCALE\" T))", Log(session));
        }

        [Fact]
        public void TheSetvarCommand_RaisesTheSameEventsOnceTheValueIsSet()
        {
            CadCommandSession session = Logging();
            Run(session, "(vlr-sysvar-reactor nil '((:vlr-sysVarWillChange . cb) (:vlr-sysVarChanged . cb)))");

            Run(session, "SETVAR", "LTSCALE", "2");

            Assert.Equal("((:VLR-sysVarWillChange \"LTSCALE\") (:VLR-sysVarChanged \"LTSCALE\" T))", Log(session));
        }

        [Fact]
        public void ASetvarOfAReadOnlyVariable_RaisesNothing()
        {
            CadCommandSession session = Logging();
            Run(session, "(vlr-sysvar-reactor nil '((:vlr-sysVarWillChange . cb) (:vlr-sysVarChanged . cb)))");

            session.Lisp.Run("(vl-catch-all-apply 'setvar '(\"DWGNAME\" \"x\"))");

            Assert.Equal("nil", Log(session));
        }

        [Fact]
        public void ASave_RaisesBeginSaveBeforeAndSaveCompleteAfterWithTheFullPath()
        {
            string path = Path.Combine(Path.GetTempPath(), "reactor-save-" + Guid.NewGuid().ToString("N") + ".dxf");

            try
            {
                CadCommandSession session = Logging();
                Run(session, "(vlr-dwg-reactor nil '((:vlr-beginSave . cb) (:vlr-saveComplete . cb)))");

                session.Document.SaveAs(path);

                string full = Path.GetFullPath(path).Replace("\\", "\\\\");
                Assert.Equal("((:VLR-beginSave \"" + full + "\") (:VLR-saveComplete \"" + full + "\"))", Log(session));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void TheClosingOfTheDocument_RaisesBeginCloseForDwgAndEditorReactors()
        {
            CadCommandSession session = Logging();
            Run(
                session,
                "(vlr-dwg-reactor nil '((:vlr-beginClose . cb)))",
                "(vlr-editor-reactor nil '((:vlr-beginClose . cb)))"
            );

            session.RaiseClosing();

            Assert.Equal("((:VLR-beginClose) (:VLR-beginClose))", Log(session));
        }

        #endregion

        #region Object and database events

        private const string Line = "(setq e (entmakex '((0 . \"LINE\") (10 0.0 0.0 0.0) (11 10.0 0.0 0.0))))";

        [Fact]
        public void AnObjectReactor_HearsAnEntmodOfItsOwnerButNotOfAnotherObject()
        {
            CadCommandSession session = Logging();
            Run(
                session,
                "(vl-load-com)",
                Line,
                "(setq f (entmakex '((0 . \"LINE\") (10 0.0 5.0 0.0) (11 10.0 5.0 0.0))))",
                "(defun cbo (n r a) (setq log (cons (cons (vlr-current-reaction-name) (list (vla-get-Handle n) a)) log)))",
                "(vlr-object-reactor (list (vlax-ename->vla-object e)) nil '((:vlr-modified . cbo)))"
            );

            Run(session, "(entmod (subst (cons 11 '(20.0 0.0 0.0)) (assoc 11 (entget e)) (entget e)))");
            Run(session, "(entmod (subst (cons 11 '(20.0 5.0 0.0)) (assoc 11 (entget f)) (entget f)))");
            Assert.Equal("((:VLR-modified " + Eval(session, "(cdr (assoc 5 (entget e)))") + " nil))", Log(session));
        }

        [Fact]
        public void AnObjectReactor_HearsAnEraseAndTheUndoOfItAsErasedAndUnerased()
        {
            CadCommandSession session = Logging();
            Run(
                session,
                "(vl-load-com)",
                Line,
                "(vlr-object-reactor (list (vlax-ename->vla-object e)) nil '((:vlr-erased . cbo) (:vlr-unerased . cbo)))"
            );
            string handle = Assert.IsType<string>(session.Lisp.Run("(cdr (assoc 5 (entget e)))").Value);

            Run(session, "ERASE", new[] { handle }, Enter);
            Assert.Equal("((:VLR-erased))", Log(session));

            Run(session, "U");
            Assert.Equal("((:VLR-erased) (:VLR-unerased))", Log(session));
        }

        [Fact]
        public void AnEraseUndoneAndRedone_RaisesErasedThenUnerasedThenErasedAgain()
        {
            CadCommandSession session = Logging();
            Run(
                session,
                "(vl-load-com)",
                Line,
                "(vlr-object-reactor (list (vlax-ename->vla-object e)) nil '((:vlr-erased . cbo) (:vlr-unerased . cbo)))"
            );
            string handle = Assert.IsType<string>(session.Lisp.Run("(cdr (assoc 5 (entget e)))").Value);

            Run(session, "ERASE", new[] { handle }, Enter, "U", "REDO");

            Assert.Equal("((:VLR-erased) (:VLR-unerased) (:VLR-erased))", Log(session));
        }

        [Fact]
        public void AnObjectReactorWhoseOwnerIsErased_StaysAdded()
        {
            CadCommandSession session = Logging();
            Run(
                session,
                "(vl-load-com)",
                Line,
                "(setq r (vlr-object-reactor (list (vlax-ename->vla-object e)) nil '((:vlr-erased . cb))))",
                "(entdel e)"
            );

            Assert.Equal("T", Eval(session, "(vlr-added-p r)"));
            Assert.Equal("1", Eval(session, "(length (vlr-owners r))"));
        }

        [Fact]
        public void ADatabaseReactor_HearsAnEntityAppendedModifiedErasedAndUnerased()
        {
            CadCommandSession session = Logging();
            Run(
                session,
                "(vlr-acdb-reactor nil '((:vlr-objectAppended . cb) (:vlr-objectModified . cb) (:vlr-objectErased . cb) (:vlr-objectUnErased . cb)))"
            );

            Run(session, Line);
            string handle = Assert.IsType<string>(session.Lisp.Run("(cdr (assoc 5 (entget e)))").Value);
            Assert.Equal(
                1,
                Eval(session, "(reverse log)").Split(new[] { ":VLR-objectAppended" }, StringSplitOptions.None).Length
                    - 1
            );

            Run(
                session,
                "(setq log nil)",
                "(entmod (subst (cons 11 '(20.0 0.0 0.0)) (assoc 11 (entget e)) (entget e)))"
            );
            Assert.StartsWith("((:VLR-objectModified #<VLA-OBJECT IAcadLine ", Log(session), StringComparison.Ordinal);

            Run(session, "(setq log nil)", "ERASE", new[] { handle }, Enter);
            Assert.StartsWith("((:VLR-objectErased #<VLA-OBJECT IAcadLine ", Log(session), StringComparison.Ordinal);

            Run(session, "(setq log nil)", "U");
            Assert.StartsWith("((:VLR-objectUnErased #<VLA-OBJECT IAcadLine ", Log(session), StringComparison.Ordinal);
        }

        #endregion

        #region Two documents

        [Fact]
        public void TwoDocuments_KeepTheirReactorsApart()
        {
            CadCommandSession first = Logging(Blank());
            CadCommandSession second = Logging(Blank());
            Run(first, "(vlr-command-reactor nil '((:vlr-commandWillStart . cb)))");

            Run(second, "LINE", Esc);
            Assert.Equal("nil", Log(second));
            Assert.Equal("nil", Log(first));

            Run(first, "LINE", Esc);
            Assert.Equal("((:VLR-commandWillStart \"LINE\"))", Log(first));
            Assert.Equal("nil", Log(second));
        }

        #endregion
    }
}
