using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Cad.Dcl;
using GeometryHelper.Cad.Lisp;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The 22 dialog functions of AutoLISP and the dialogs they run (L5 part E, D314-D330 and section 2c). The dialogs are
    /// written in DCL, in files made in a folder of the test's own, loaded by <c>load_dialog</c> and driven by the script host
    /// (<see cref="DclScriptHost"/>), which logs what it did. Rows are marked as the spec asks: <c>AutoCAD 2021 (d1)</c> for
    /// what the console measured (no dialog can open there), <c>AutoCAD 2021 (GUI g1)</c> for what the full AutoCAD did,
    /// <c>reference</c> for what the AutoCAD 2021 help says, <c>ours: D3nn</c> for the spec's own choice.
    /// </summary>
    public sealed class LispDialogTests : IDisposable
    {
        // The dialogs of every test, written in words of the test's own.
        private const string Kit = """
            ghok : dialog { label = "Only"; : text { label = "x"; } ok_only; }
            ghoc : dialog {
                label = "OK Cancel";
                : edit_box { key = "eb"; label = "Name:"; value = "abc"; edit_width = 10; }
                ok_cancel;
            }
            ghedit : dialog {
                label = "Edit text"; initial_focus = "text";
                : edit_box { label = "Text:"; key = "text"; edit_width = 20; allow_accept = true; }
                ok_cancel;
            }
            ghplain : dialog {
                label = "Plain edit";
                : edit_box { label = "Text:"; key = "text"; edit_width = 20; }
                ok_cancel;
            }
            ghlimit : dialog {
                label = "Limit";
                : edit_box { label = "E:"; key = "e"; edit_limit = 5; }
                ok_cancel;
            }
            ghlist : dialog {
                label = "List";
                : list_box { key = "lst"; list = "a\nb\nc"; allow_accept = true; }
                ok_cancel;
            }
            ghbtn : dialog {
                label = "Buttons";
                : button { key = "b1"; label = "One"; }
                : button { key = "b2"; label = "Two"; is_enabled = false; }
                ok_cancel;
            }
            ghnoexit : dialog { label = "No exit"; : button { key = "b"; label = "B"; } }
            ghvar : dialog {
                label = "Variables";
                : button { key = "bv"; label = "Button"; }
                : toggle { key = "tg"; label = "Toggle"; }
                : edit_box { key = "eb"; label = "Edit:"; edit_width = 8; }
                : radio_row {
                    key = "rc";
                    : radio_button { key = "r1"; label = "One"; }
                    : radio_button { key = "r2"; label = "Two"; }
                }
                ok_cancel;
            }
            ghall : dialog {
                label = "Every kind";
                : edit_box { key = "txt"; label = "&Text:"; edit_width = 20; value = "abc"; allow_accept = true; }
                : edit_box { key = "txd"; label = "No value"; }
                : popup_list { key = "pop"; label = "P"; list = "a\nb\nc"; }
                : popup_list { key = "pop2"; list = "a\nb\nc"; value = "1"; }
                : popup_list { key = "popn"; }
                : toggle { key = "tg"; label = "T"; value = "1"; }
                : toggle { key = "tg0"; label = "T0"; }
                : radio_column {
                    key = "rc";
                    : radio_button { key = "r1"; label = "R1"; }
                    : radio_button { key = "r2"; label = "R2"; value = "1"; }
                }
                : slider { key = "sl"; min_value = 0; max_value = 100; value = 25; }
                : slider { key = "sl0"; }
                : text { key = "tx"; label = "A text label"; }
                : text { key = "tx2"; label = "Label"; value = "Value"; }
                : list_box { key = "lst"; label = "L"; list = "a\nb\nc\nd\ne"; multiple_select = true; width = 20; height = 6; }
                : list_box { key = "lst1"; list = "a\nb\nc"; }
                : image { key = "img"; width = 10; height = 4; }
                : image_button { key = "ib"; width = 6; height = 2; }
                : button { key = "btn"; label = "B"; }
                errtile;
                ok_cancel;
            }
            """;

        private readonly string _folder = Path.Combine(
            Path.GetTempPath(),
            "gh-lisp-dialog-" + Guid.NewGuid().ToString("N")
        );

        private readonly string _path;

        public LispDialogTests()
        {
            Directory.CreateDirectory(_folder);
            _path = Path.Combine(_folder, "kit.dcl");
            File.WriteAllText(_path, Kit, new UTF8Encoding(false));
        }

        public void Dispose()
        {
            WaitingRoutines.EndAll();

            try
            {
                Directory.Delete(_folder, true);
            }
            catch (IOException) { }
        }

        /// <summary>A host that keeps the dialogs it was given and lets a script drive them.</summary>
        private sealed class Shown : IDclHost
        {
            private readonly DclScriptHost _script;

            internal Shown(params string[] steps)
            {
                _script = new DclScriptHost(steps);
            }

            internal List<DclDialog> Dialogs { get; } = new List<DclDialog>();

            internal IReadOnlyList<string> Log => _script.Log;

            public void Open(DclDialog dialog)
            {
                Dialogs.Add(dialog);
                _script.Open(dialog);
            }

            public DclEvent Next(DclDialog dialog) => _script.Next(dialog);

            public void Close(DclDialog dialog) => _script.Close(dialog);
        }

        // A session whose routines have a script to drive their dialogs.
        private static CadCommandSession With(out Shown host, params string[] steps)
        {
            CadCommandSession session = Session(Blank());
            host = new Shown(steps);
            session.DclHost = host;
            return session;
        }

        // The kit's path as a routine writes it in a string.
        private string Slashed(string path) => path.Replace('\\', '/');

        // Runs text with @P standing for the kit's path; the last value as prin1 writes it, or the error.
        private string Eval(CadCommandSession session, string code)
        {
            LispResult result = session.Lisp.Run(code.Replace("@P", Slashed(_path)));
            return result.Failed ? "error: " + result.Error : LispInterpreter.Print(result.Value, true);
        }

        // The kit loaded and the dialog made, then the code.
        private string Dialog(CadCommandSession session, string name, string code) =>
            Eval(session, "(setq id (load_dialog \"@P\")) (new_dialog \"" + name + "\" id) " + code);

        private string WriteFile(string name, string text)
        {
            string path = Path.Combine(_folder, name);
            File.WriteAllText(path, text, new UTF8Encoding(false));
            return path;
        }

        #region No dialog: what the console measured

        [Fact]
        public void TheTwentyTwoFunctions_AreSubrs()
        {
            // AutoCAD 2021 (d1): every one is a SUBR
            string[] names =
            {
                "load_dialog",
                "unload_dialog",
                "new_dialog",
                "start_dialog",
                "done_dialog",
                "term_dialog",
                "action_tile",
                "get_tile",
                "set_tile",
                "mode_tile",
                "get_attr",
                "client_data_tile",
                "start_list",
                "add_list",
                "end_list",
                "start_image",
                "fill_image",
                "vector_image",
                "slide_image",
                "end_image",
                "dimx_tile",
                "dimy_tile",
            };
            CadCommandSession session = Session(Blank());

            string types = Eval(session, "(mapcar '(lambda (s) (type (eval s))) '(" + string.Join(" ", names) + "))");

            Assert.Equal("(" + string.Join(" ", names.Select(n => "SUBR")) + ")", types);
        }

        [Fact]
        public void ABareInterpreter_HasNoDialogFunctions()
        {
            // ours: D314 (as command and ssget, they need the session)
            var lisp = new LispInterpreter();

            Assert.True(lisp.Run("(new_dialog \"a\" 1)").Failed);
            Assert.Equal("no function definition: NEW_DIALOG", lisp.Run("(new_dialog \"a\" 1)").Error);
        }

        [Theory]
        [InlineData("""(load_dialog 5)""", "bad argument type: stringp 5")]
        [InlineData("""(load_dialog)""", "too few arguments")]
        [InlineData("""(load_dialog "a" "b")""", "too many arguments")]
        [InlineData("""(load_dialog 'abc)""", "bad argument type: stringp ABC")]
        [InlineData("""(load_dialog nil)""", "bad argument type: stringp nil")]
        [InlineData("""(new_dialog 5 1)""", "bad argument type: stringp 5")]
        [InlineData("""(new_dialog "ghgood" "x")""", """bad argument type: fixnump: "x" """)]
        [InlineData("""(new_dialog "ghgood")""", "too few arguments")]
        [InlineData("""(new_dialog "a" 1.5)""", "bad argument type: fixnump: 1.5")]
        [InlineData("""(new_dialog "a" -1 "" 5)""", "bad argument value: dialog position: 5")]
        [InlineData("""(new_dialog "a" -1 5)""", "bad argument type: stringp 5")]
        [InlineData("""(new_dialog "a" -1 "" '(1 2) 7)""", "too many arguments")]
        [InlineData("""(get_tile 5)""", "bad argument type: stringp 5")]
        [InlineData("""(get_tile)""", "too few arguments")]
        [InlineData("""(get_tile "a" "b")""", "too many arguments")]
        [InlineData("""(set_tile 5 "a")""", "bad argument type: stringp 5")]
        [InlineData("""(set_tile "a")""", "too few arguments")]
        [InlineData("""(set_tile "txt" 5)""", "bad argument type: stringp 5")]
        [InlineData("""(action_tile 5 "x")""", "bad argument type: stringp 5")]
        [InlineData("""(action_tile "a")""", "too few arguments")]
        [InlineData("""(action_tile "accept" 5)""", "bad argument type: stringp 5")]
        [InlineData("""(mode_tile 5 1)""", "bad argument type: stringp 5")]
        [InlineData("""(mode_tile "a" 1.5)""", "bad argument type: fixnump: 1.5")]
        [InlineData("""(mode_tile "txt" "a")""", """bad argument type: fixnump: "a" """)]
        [InlineData("""(mode_tile "a")""", "too few arguments")]
        [InlineData("""(get_attr 5 "label")""", "bad argument type: stringp 5")]
        [InlineData("""(get_attr "a")""", "too few arguments")]
        [InlineData("""(get_attr "txt" 5)""", "bad argument type: stringp 5")]
        [InlineData("""(client_data_tile 5 "d")""", "bad argument type: stringp 5")]
        [InlineData("""(client_data_tile "a" 5)""", "bad argument type: stringp 5")]
        [InlineData("""(client_data_tile "a" '(1 2))""", "bad argument type: stringp (1 2)")]
        [InlineData("""(start_list 5)""", "bad argument type: stringp 5")]
        [InlineData("""(start_list "a" "b")""", """bad argument type: fixnump: "b" """)]
        [InlineData("""(start_list "a" 1.5)""", "bad argument type: fixnump: 1.5")]
        [InlineData("""(start_list "a" 2 "x")""", """bad argument type: fixnump: "x" """)]
        [InlineData("""(start_list "a" 1 0 0)""", "too many arguments")]
        [InlineData("""(start_list)""", "too few arguments")]
        [InlineData("""(add_list 5)""", "bad argument type: stringp 5")]
        [InlineData("""(add_list)""", "too few arguments")]
        [InlineData("""(add_list "a" "b")""", "too many arguments")]
        [InlineData("""(end_list 1)""", "too many arguments")]
        [InlineData("""(start_image 5)""", "bad argument type: stringp 5")]
        [InlineData("""(start_image)""", "too few arguments")]
        [InlineData("""(fill_image 0 0 10 10 "a")""", """bad argument type: fixnump: "a" """)]
        [InlineData("""(fill_image 0 0 10 10)""", "too few arguments")]
        [InlineData("""(fill_image 0.5 0 10 10 1)""", "bad argument type: fixnump: 0.5")]
        [InlineData("""(fill_image "a" 0 10 10 1)""", """bad argument type: fixnump: "a" """)]
        [InlineData("""(vector_image 0 0 10 10)""", "too few arguments")]
        [InlineData("""(vector_image 0 0 10 10 1.5)""", "bad argument type: fixnump: 1.5")]
        [InlineData("""(slide_image 0 0 10 10 5)""", "bad argument type: stringp 5")]
        [InlineData("""(slide_image 0 0 10 10)""", "too few arguments")]
        [InlineData("""(end_image 1)""", "too many arguments")]
        [InlineData("""(dimx_tile 5)""", "bad argument type: stringp 5")]
        [InlineData("""(dimx_tile)""", "too few arguments")]
        [InlineData("""(dimy_tile 5)""", "bad argument type: stringp 5")]
        [InlineData("""(done_dialog "a")""", """bad argument type: fixnump: "a" """)]
        [InlineData("""(done_dialog 1.5)""", "bad argument type: fixnump: 1.5")]
        [InlineData("""(done_dialog 1 2)""", "too many arguments")]
        [InlineData("""(term_dialog 1)""", "too many arguments")]
        [InlineData("""(start_dialog 1)""", "too many arguments")]
        [InlineData("""(unload_dialog)""", "too few arguments")]
        [InlineData("""(unload_dialog 1.0)""", "bad argument type: fixnump: 1.0")]
        [InlineData("""(unload_dialog "x")""", """bad argument type: fixnump: "x" """)]
        public void AnArgumentOfTheWrongKindOrCount_IsAnErrorBeforeAnythingElse(string code, string message)
        {
            // AutoCAD 2021 (d1, d2): the same errors with no dialog, and so with one
            CadCommandSession session = Session(Blank());

            Assert.Equal("error: " + message.Trim(), Eval(session, code));
        }

        [Theory]
        [InlineData("""(get_tile "txt")""", "nil")]
        [InlineData("""(set_tile "txt" "zz")""", "nil")]
        [InlineData("""(action_tile "accept" "(done_dialog 1)")""", "nil")]
        [InlineData("""(mode_tile "txt" 1)""", "nil")]
        [InlineData("""(mode_tile "a" 9)""", "nil")]
        [InlineData("""(get_attr "txt" "label")""", "\"\"")]
        [InlineData("""(client_data_tile "txt" "data")""", "nil")]
        [InlineData("""(start_list "lst")""", "nil")]
        [InlineData("""(start_list "lst" 3 0)""", "nil")]
        [InlineData("""(add_list "x")""", "nil")]
        [InlineData("""(end_list)""", "nil")]
        [InlineData("""(start_image "img")""", "nil")]
        [InlineData("""(dimx_tile "img")""", "0")]
        [InlineData("""(dimy_tile "img")""", "0")]
        [InlineData("""(fill_image 0 0 10 10 1)""", "nil")]
        [InlineData("""(vector_image 0 0 10 10 2)""", "nil")]
        [InlineData("""(slide_image 0 0 10 10 "acad(box)")""", "nil")]
        [InlineData("""(end_image)""", "nil")]
        [InlineData("""(done_dialog)""", "(0 0)")]
        [InlineData("""(done_dialog 3)""", "(0 0)")]
        [InlineData("""(done_dialog -1)""", "(0 0)")]
        [InlineData("""(term_dialog)""", "nil")]
        [InlineData("""(start_dialog)""", "0")]
        [InlineData("""(unload_dialog 1)""", "nil")]
        [InlineData("""(unload_dialog 999)""", "nil")]
        [InlineData("""(unload_dialog -1)""", "nil")]
        public void WithNoDialog_EveryFunctionGivesTheConsolesValue(string code, string expected)
        {
            // AutoCAD 2021 (d1, d2): the values of accoreconsole, where no dialog can be made
            CadCommandSession session = Session(Blank());

            Assert.Equal(expected, Eval(session, code));
        }

        [Fact]
        public void TheDollarVariables_AreUnboundBeforeAnyDialog()
        {
            // AutoCAD 2021 (d3)
            CadCommandSession session = Session(Blank());

            Assert.Equal(
                "(nil nil nil nil nil nil)",
                Eval(
                    session,
                    "(list (boundp '$key) (boundp '$value) (boundp '$reason) (boundp '$data) (boundp '$x) (boundp '$y))"
                )
            );
        }

        [Fact]
        public void LoadDialogOfAFileThatIsNotThere_GivesMinusOne()
        {
            // AutoCAD 2021 (d1): -1 for a missing file, with or without .dcl, and for an empty name
            CadCommandSession session = Session(Blank());
            string missing = Slashed(Path.Combine(_folder, "missing"));

            Assert.Equal(
                "(-1 -1 -1)",
                Eval(
                    session,
                    "(list (load_dialog \"" + missing + ".dcl\") (load_dialog \"" + missing + "\") (load_dialog \"\"))"
                )
            );
        }

        [Fact]
        public void NewDialogWithNoHost_GivesNilWhateverTheFile()
        {
            // AutoCAD 2021 (d1): the console loads nothing and makes no dialog; ours: D316 loads the file, no host shows it
            CadCommandSession session = Session(Blank());

            Assert.Equal("(1 nil)", Eval(session, "(list (setq id (load_dialog \"@P\")) (new_dialog \"ghoc\" id))"));
        }

        #endregion

        #region load_dialog and unload_dialog

        [Fact]
        public void ThreeLoads_GiveTheIdsOneTwoThree_AWithAndWithoutTheExtension()
        {
            // AutoCAD 2021 (GUI g1): 1, 2, 3 for three loads of one file; ours: D315 (a failed load takes no id)
            CadCommandSession session = Session(Blank());
            string bare = Slashed(Path.Combine(_folder, "kit"));

            Assert.Equal(
                "(1 -1 2 3)",
                Eval(
                    session,
                    "(list (load_dialog \"@P\") (load_dialog \""
                        + bare
                        + "x\") (load_dialog \"@P\") (load_dialog \""
                        + bare
                        + "\"))"
                )
            );
        }

        [Fact]
        public void ABareName_IsLookedForInTheCurrentFolderWithDclAdded()
        {
            // reference: load_dialog searches the support path for a bare name; ours: D315 (as findfile)
            CadCommandSession session = Session(Blank());
            session.Lisp.CurrentFolder = () => _folder;

            Assert.Equal("1", Eval(session, "(load_dialog \"kit\")"));
            Assert.Equal("2", Eval(session, "(load_dialog \"kit.dcl\")"));
        }

        [Fact]
        public void ABareName_IsLookedForInTheSupportFolders()
        {
            // ours: D315
            string support = Path.Combine(_folder, "support");
            Directory.CreateDirectory(support);
            File.Copy(_path, Path.Combine(support, "mine.dcl"));
            CadCommandSession session = Session(Blank());
            session.Lisp.CurrentFolder = () => Path.Combine(_folder, "nowhere");
            session.Document.SupportFolders = new[] { support };

            Assert.Equal("1", Eval(session, "(load_dialog \"mine\")"));
        }

        [Fact]
        public void UnloadDialog_DropsTheFile_AndADialogAlreadyMadeKeepsItsTiles()
        {
            // reference: unload_dialog frees the file; ours: D315 (open dialogs keep their copy)
            CadCommandSession session = With(out Shown host, "click accept");

            string value = Eval(
                session,
                "(setq id (load_dialog \"@P\")) (new_dialog \"ghoc\" id) (unload_dialog id) (setq s (start_dialog)) (list s (new_dialog \"ghoc\" id))"
            );

            Assert.Equal("(1 nil)", value);
            Assert.Equal("open ghoc", host.Log[0]);
        }

        [Fact]
        public void AFileInUtf8_IsReadAsUtf8()
        {
            // ours: D301 (read as UTF-8 when it is UTF-8)
            string path = WriteFile("u8.dcl", "u8 : dialog { : button { key = \"accept\"; label = \"café đ\"; } }");
            CadCommandSession session = With(out Shown host);

            Assert.Equal(
                "\"café đ\"",
                Eval(
                    session,
                    "(new_dialog \"u8\" (load_dialog \"" + Slashed(path) + "\")) (get_attr \"accept\" \"label\")"
                )
            );
        }

        [Fact]
        public void AFileThatIsNotUtf8_IsReadInTheAnsiCodePage()
        {
            // ours: D301 (else the system's ANSI code page)
            Encoding ansi = Encoding.Default;

            // A code page that cannot write the letter says nothing about the loader.
            if (ansi.GetString(ansi.GetBytes("café")) != "café")
            {
                return;
            }

            string path = Path.Combine(_folder, "ansi.dcl");
            File.WriteAllBytes(path, ansi.GetBytes("a1 : dialog { : button { key = \"accept\"; label = \"café\"; } }"));
            CadCommandSession session = With(out Shown host);

            Assert.Equal(
                "\"café\"",
                Eval(
                    session,
                    "(new_dialog \"a1\" (load_dialog \"" + Slashed(path) + "\")) (get_attr \"accept\" \"label\")"
                )
            );
        }

        [Fact]
        public void AFileWithASyntaxError_ShowsItsMessageAndGivesMinusOne()
        {
            // AutoCAD 2021 (GUI g1): -1 and a message box; ours: D307, D338 (the message goes to the alert)
            string path = WriteFile("bad.dcl", "bad : dialog {\n  : row { }\n  label = ;\n}\n");
            CadCommandSession session = Session(Blank());

            string value = Eval(session, "(load_dialog \"" + Slashed(path) + "\")");

            Assert.Equal("-1", value);
            string history = string.Join("\n", session.History);
            Assert.Contains("Error in dialog file", history);
            Assert.Contains("syntax error", history);
        }

        [Fact]
        public void AnIncludeThatIsNotThere_GivesMinusOneWithItsMessage()
        {
            // AutoCAD 2021 (GUI g1): -1; ours: D307 (the include's message)
            string path = WriteFile("inc.dcl", "@include \"nothere.dcl\";\nx : dialog { }\n");
            CadCommandSession session = Session(Blank());

            Assert.Equal("-1", Eval(session, "(load_dialog \"" + Slashed(path) + "\")"));
            Assert.Contains("not found", string.Join("\n", session.History));
        }

        [Fact]
        public void AnIncludeBesideTheFile_IsReadAndItsDialogIsUsable()
        {
            // ours: D305 (the including file's folder first)
            WriteFile("parts.dcl", "mybutton : button { label = \"Mine\"; key = \"mine\"; }\n");
            string path = WriteFile(
                "main.dcl",
                "@include \"parts\";\nmain : dialog { label = \"M\"; : mybutton { } ok_cancel; }\n"
            );
            CadCommandSession session = With(out Shown host);

            Assert.Equal(
                "\"Mine\"",
                Eval(
                    session,
                    "(new_dialog \"main\" (load_dialog \"" + Slashed(path) + "\")) (get_attr \"mine\" \"label\")"
                )
            );
        }

        #endregion

        #region new_dialog

        [Fact]
        public void NewDialog_WithAHost_GivesTAndTheDialogIsCurrent()
        {
            CadCommandSession session = With(out Shown host);

            // reference: new_dialog gives T and makes the dialog the current one; ours: D316 (not shown until start_dialog)
            Assert.Equal(
                "(T \"abc\")",
                Eval(session, "(setq id (load_dialog \"@P\")) (list (new_dialog \"ghoc\" id) (get_tile \"eb\"))")
            );
            Assert.Empty(host.Dialogs);
        }

        [Fact]
        public void NewDialog_OfANameThatIsNoDialogOrAnIdNoFileHas_GivesNilAndNoMessage()
        {
            // AutoCAD 2021 (GUI g1): nil for an unknown name, id 999 and -1, no message
            CadCommandSession session = With(out Shown host);

            Assert.Equal(
                "(nil nil nil nil)",
                Eval(
                    session,
                    "(setq id (load_dialog \"@P\")) (list (new_dialog \"nosuch\" id) (new_dialog \"ghoc\" 999) (new_dialog \"ghoc\" -1) (new_dialog \"ok_cancel\" id))"
                )
            );
            Assert.Empty(session.History);
        }

        [Fact]
        public void NewDialog_OfADialogWithNoTileThatCanEndIt_GivesNil()
        {
            // AutoCAD 2021 (GUI g1): nil and no message for a dialog with no exit tile
            CadCommandSession session = With(out Shown host);

            Assert.Equal("nil", Eval(session, "(new_dialog \"ghnoexit\" (load_dialog \"@P\"))"));
        }

        [Fact]
        public void ANewDialogMadeBeforeTheLastWasShown_TakesItsPlace()
        {
            // ours: D317 (the current dialog is the newest one)
            CadCommandSession session = With(out Shown host, "esc");

            Assert.Equal(
                "(nil \"\")",
                Eval(
                    session,
                    "(setq id (load_dialog \"@P\")) (new_dialog \"ghoc\" id) (new_dialog \"ghedit\" id) (list (get_tile \"eb\") (get_tile \"text\"))"
                )
            );
            Assert.Equal("0", Eval(session, "(start_dialog)"));
            Assert.Equal("open ghedit", host.Log[0]);
        }

        [Fact]
        public void TheTilesOfANewDialog_HoldTheValuesTheDclGives()
        {
            // AutoCAD 2021 (GUI g1): the values before start_dialog
            CadCommandSession session = With(out Shown host);
            string[] keys =
            {
                "txt",
                "txd",
                "pop",
                "pop2",
                "popn",
                "tg",
                "tg0",
                "rc",
                "r1",
                "r2",
                "sl",
                "sl0",
                "tx",
                "tx2",
                "lst",
                "lst1",
                "img",
                "ib",
                "btn",
                "accept",
                "error",
                "nokey",
                "Txt",
            };

            string value = Dialog(
                session,
                "ghall",
                "(mapcar 'get_tile '(" + string.Join(" ", keys.Select(k => "\"" + k + "\"")) + "))"
            );

            Assert.Equal(
                "(\"abc\" \"\" \"0\" \"1\" \"\" \"1\" \"0\" \"r2\" \"0\" \"1\" \"25\" \"0\" \"A text label\" \"Value\" \"\" \"\" nil nil nil nil \"\" nil nil)",
                value
            );
        }

        [Fact]
        public void GetAttr_GivesTheTextAsWrittenInTheDcl()
        {
            // AutoCAD 2021 (GUI g1): the text as written, a prototype's too, "" where it is missing
            CadCommandSession session = With(out Shown host);

            string value = Dialog(
                session,
                "ghall",
                "(list (get_attr \"txt\" \"label\") (get_attr \"txt\" \"edit_width\") (get_attr \"txt\" \"allow_accept\") (get_attr \"txt\" \"key\") (get_attr \"txt\" \"nosuch\") (get_attr \"accept\" \"label\") (get_attr \"accept\" \"width\") (get_attr \"lst\" \"list\") (get_attr \"nokey\" \"label\"))"
            );

            Assert.Equal(
                "(\"&Text:\" \"20\" \"true\" \"txt\" \"\" \"  OK  \" \"8\" \"a\\nb\\nc\\nd\\ne\" \"\")",
                value
            );
        }

        [Fact]
        public void DimxAndDimy_GiveTheTilesLaidOutSizeInWholePixels_AndZeroForAMissingKey()
        {
            // AutoCAD 2021 (GUI g1): the laid-out size, 0 for a missing key; the numbers are the layout's (part P)
            CadCommandSession session = With(out Shown host, "esc");

            string value = Dialog(
                session,
                "ghall",
                "(setq w (dimx_tile \"img\") h (dimy_tile \"img\") m (dimx_tile \"nokey\")) (start_dialog) (list w h m)"
            );

            DclDialog dialog = host.Dialogs.Single();
            DclRect rect = DclLayout.Arrange(dialog.Root, DclMetrics.Default)[dialog.Find("img")];
            Assert.Equal("(" + (int)Math.Floor(rect.Width) + " " + (int)Math.Floor(rect.Height) + " 0)", value);
        }

        #endregion

        #region Values: set_tile, get_tile, lists

        [Fact]
        public void SetTile_GivesTheValueGiven_AndNilForAMissingKey()
        {
            // AutoCAD 2021 (GUI g1): the value given; a button keeps nothing; nil for a missing key
            CadCommandSession session = With(out Shown host);

            string value = Dialog(
                session,
                "ghall",
                "(list (set_tile \"txt\" \"zz\") (get_tile \"txt\") (set_tile \"nokey\" \"x\") (set_tile \"btn\" \"x\") (get_tile \"btn\") (set_tile \"tx\" \"Changed\") (get_tile \"tx\") (get_attr \"tx\" \"label\"))"
            );

            Assert.Equal("(\"zz\" \"zz\" nil \"x\" nil \"Changed\" \"Changed\" \"A text label\")", value);
        }

        [Fact]
        public void ARadioButtonSetToOne_MovesTheClusterValue_AndTheClusterSetToAKeyMovesTheButtons()
        {
            // AutoCAD 2021 (GUI g1)
            CadCommandSession session = With(out Shown host);

            string value = Dialog(
                session,
                "ghall",
                "(set_tile \"r1\" \"1\") (setq a (list (get_tile \"rc\") (get_tile \"r1\") (get_tile \"r2\"))) (set_tile \"rc\" \"r2\") (list a (get_tile \"rc\") (get_tile \"r1\") (get_tile \"r2\"))"
            );

            Assert.Equal("((\"r1\" \"1\" \"0\") \"r2\" \"0\" \"1\")", value);
        }

        [Fact]
        public void ASliderSetOutOfItsLimits_IsClamped_AndAToggleKeepsAnyText()
        {
            // AutoCAD 2021 (GUI g1): 500 gives 100, -5 gives 0; a toggle keeps "7"
            CadCommandSession session = With(out Shown host);

            string value = Dialog(
                session,
                "ghall",
                "(set_tile \"sl\" \"500\") (setq a (get_tile \"sl\")) (set_tile \"sl\" \"-5\") (setq b (get_tile \"sl\")) (set_tile \"tg\" \"7\") (list a b (get_tile \"tg\"))"
            );

            Assert.Equal("(\"100\" \"0\" \"7\")", value);
        }

        [Fact]
        public void ASetTileOfAMultipleList_AddsToTheChoice_IgnoresWhatIsOutOfRange_AndEmptyClears()
        {
            // AutoCAD 2021 (GUI g1): "0 2", then "4 1" gives "0 1 2 4", "9" ignored, "" clears
            CadCommandSession session = With(out Shown host);

            string value = Dialog(
                session,
                "ghall",
                "(set_tile \"lst\" \"0 2\") (setq a (get_tile \"lst\")) (set_tile \"lst\" \"4 1\") (setq b (get_tile \"lst\")) (set_tile \"lst\" \"9\") (setq c (get_tile \"lst\")) (set_tile \"lst\" \"\") (list a b c (get_tile \"lst\"))"
            );

            Assert.Equal("(\"0 2\" \"0 1 2 4\" \"0 1 2 4\" \"\")", value);
        }

        [Fact]
        public void ASetTileOfASingleList_TakesOneIndex_AndAPopupTakesAnIndexInRange()
        {
            // ours: D318 (a single list takes the first index given; a popup ignores an index it has no item for)
            CadCommandSession session = With(out Shown host);

            string value = Dialog(
                session,
                "ghall",
                "(set_tile \"lst1\" \"2 1\") (setq a (get_tile \"lst1\")) (set_tile \"lst1\" \"\") (setq b (get_tile \"lst1\")) (set_tile \"pop\" \"2\") (setq c (get_tile \"pop\")) (set_tile \"pop\" \"7\") (list a b c (get_tile \"pop\"))"
            );

            Assert.Equal("(\"1\" \"\" \"2\" \"2\")", value);
        }

        [Fact]
        public void StartList_Operation3_And2_KeepAPopupsValue_AndOperation1ClearsAReplacedChoice()
        {
            // AutoCAD 2021 (GUI g1): op 3 keeps the "0", op 2 too, op 1 replacing item 0 makes it ""
            CadCommandSession session = With(out Shown host);

            string value = Dialog(
                session,
                "ghall",
                "(start_list \"pop\") (add_list \"delta\") (add_list \"eps\") (end_list) (setq a (get_tile \"pop\")) (start_list \"pop\" 2) (add_list \"zeta\") (end_list) (setq b (get_tile \"pop\")) (start_list \"pop\" 1 0) (add_list \"ALPHA\") (end_list) (list a b (get_tile \"pop\"))"
            );

            Assert.Equal("(\"0\" \"0\" \"\")", value);
        }

        [Fact]
        public void TheItemsOfAList_AreWhatStartListAndAddListMade()
        {
            // reference: operation 3 starts a new list, 2 appends, 1 replaces the item at the index
            CadCommandSession session = With(out Shown host, "esc");

            string value = Dialog(
                session,
                "ghall",
                "(start_list \"lst1\") (add_list \"x\") (add_list \"y\") (end_list) (start_list \"lst1\" 2) (add_list \"z\") (end_list) (start_list \"lst1\" 1 0) (add_list \"X\") (end_list) (start_dialog)"
            );

            Assert.Equal("0", value);
            Assert.Equal(new[] { "X", "y", "z" }, host.Dialogs.Single().Find("lst1").Items);
        }

        [Fact]
        public void StartList_GivesTheKeyOfAnyTile_NilForAMissingOne_AndAddListWithNoListGivesNil()
        {
            // AutoCAD 2021 (GUI g1): the key for any keyed tile (an edit box too), nil for a missing key; add_list gives the string
            CadCommandSession session = With(out Shown host);

            string value = Dialog(
                session,
                "ghall",
                "(list (start_list \"nokey\") (add_list \"q\") (end_list) (start_list \"txt\") (add_list \"q\") (end_list) (add_list \"r\"))"
            );

            Assert.Equal("(nil nil nil \"txt\" \"q\" nil nil)", value);
        }

        [Fact]
        public void TheChoiceOfAList_DropsTheIndicesThatNoLongerExist()
        {
            // reference: a new list drops the choices past its end; ours: D319
            CadCommandSession session = With(out Shown host);

            string value = Dialog(
                session,
                "ghall",
                "(set_tile \"lst\" \"0 4\") (start_list \"lst\") (add_list \"a\") (add_list \"b\") (end_list) (get_tile \"lst\")"
            );

            Assert.Equal("\"0\"", value);
        }

        #endregion

        #region Images

        [Fact]
        public void TheOperationsOfAnImage_AreKeptInOrder_AndAFillOfTheWholeTileClearsTheOnesBefore()
        {
            // AutoCAD 2021 (GUI g1): start_image gives the key, fill_image and vector_image the colour, end_image nil;
            // ours: D320 (a fill that covers the whole tile clears the earlier operations)
            CadCommandSession session = With(out Shown host, "esc");

            string value = Dialog(
                session,
                "ghall",
                "(setq a (list (start_image \"img\") (vector_image 1 2 3 4 1) (fill_image 5 6 7 8 3) (end_image))) (start_image \"img\") (fill_image 0 0 (dimx_tile \"img\") (dimy_tile \"img\") 2) (vector_image 0 0 10 10 1) (end_image) (start_dialog) a"
            );

            Assert.Equal("(\"img\" 1 3 nil)", value);
            DclImageOp[] picture = host.Dialogs.Single().Find("img").Picture.ToArray();
            Assert.Equal(2, picture.Length);
            Assert.Equal(DclImageOpKind.Fill, picture[0].Kind);
            Assert.Equal(2, picture[0].Color);
            Assert.Equal(DclImageOpKind.Vector, picture[1].Kind);
            Assert.Equal(0, picture[1].X1);
            Assert.Equal(0, picture[1].Y1);
            Assert.Equal(10, picture[1].X2);
            Assert.Equal(10, picture[1].Y2);
            Assert.Equal(1, picture[1].Color);
        }

        [Fact]
        public void StartImage_OfAMissingKey_GivesTheKey_AndDrawsNowhere()
        {
            // AutoCAD 2021 (GUI g1): start_image of a missing key still gives the key
            CadCommandSession session = With(out Shown host);

            Assert.Equal(
                "(\"nokey\" 4 nil)",
                Dialog(session, "ghall", "(list (start_image \"nokey\") (fill_image 0 0 5 5 4) (end_image))")
            );
        }

        [Fact]
        public void SlideImage_DrawsNothing_AndSaysSoOncePerDialog()
        {
            // ours: D320 (slides are not read yet: refused, not an error)
            CadCommandSession session = With(out Shown host);
            session.Lisp.Run("(setq id (load_dialog \"" + Slashed(_path) + "\"))");
            session.Lisp.Run("(new_dialog \"ghall\" id) (start_image \"img\")");

            LispResult first = session.Lisp.Run("(slide_image 0 0 10 10 \"box\") (slide_image 0 0 10 10 \"box\")");

            Assert.False(first.Failed);
            Assert.Equal(
                1,
                first
                    .Output.Split('\n')
                    .Count(l =>
                        l.StartsWith(
                            "slide_image: slides are not supported by GeometryHelper.Cad yet",
                            StringComparison.Ordinal
                        )
                    )
            );
            Assert.Empty(host.Dialogs);
        }

        #endregion

        #region Exits

        [Fact]
        public void OkWithNoAction_EndsTheDialogWithOne()
        {
            // AutoCAD 2021 (GUI g2): OK with no action gives 1
            CadCommandSession session = With(out Shown host, "click accept");

            Assert.Equal("1", Dialog(session, "ghoc", "(start_dialog)"));
            Assert.Equal(new[] { "open ghoc", "dump ghoc eb=\"abc\" accept=nil cancel=nil", "close ghoc 1" }, host.Log);
        }

        [Fact]
        public void CancelAndEscWithNoAction_EndTheDialogWithZero()
        {
            // AutoCAD 2021 (GUI g2): Cancel clicked, and Esc, with no action give 0
            CadCommandSession session = With(out Shown host, "click cancel", "esc");

            Assert.Equal(
                "(0 0)",
                Dialog(session, "ghoc", "(setq a (start_dialog)) (new_dialog \"ghoc\" id) (list a (start_dialog))")
            );
        }

        [Fact]
        public void OkOnlyAndEsc_PressesItsOkWhichClosesWithOne()
        {
            // AutoCAD 2021 (GUI g2): ok_only and Esc: its OK (is_cancel, key accept) closes with 1
            CadCommandSession session = With(out Shown host, "esc");

            Assert.Equal("1", Dialog(session, "ghok", "(start_dialog)"));
        }

        [Fact]
        public void DoneDialogWithNoStatus_GivesZero_EvenFromTheOkAction()
        {
            // AutoCAD 2021 (GUI g2): an OK whose action calls done_dialog with no status closes with 0
            CadCommandSession session = With(out Shown host, "click accept");

            Assert.Equal("0", Dialog(session, "ghoc", "(action_tile \"accept\" \"(done_dialog)\") (start_dialog)"));
        }

        [Fact]
        public void DoneDialogWithAStatus_IsWhatStartDialogGives()
        {
            // reference: done_dialog's status is start_dialog's value
            CadCommandSession session = With(out Shown host, "click b1");

            Assert.Equal("7", Dialog(session, "ghbtn", "(action_tile \"b1\" \"(done_dialog 7)\") (start_dialog)"));
        }

        [Fact]
        public void ACancelActionThatDoesNotCallDoneDialog_ClosesTheDialogWithZeroAnyway()
        {
            // AutoCAD 2021 (GUI g1, g2): Esc and a Cancel click run cancel's action and close with 0 (E1's case)
            CadCommandSession session = With(out Shown host, "type eb \"NEW\"", "esc");

            string value = Dialog(
                session,
                "ghoc",
                "(action_tile \"cancel\" \"(setq C1 (list (get_tile \\\"eb\\\")))\") (list (start_dialog) C1)"
            );

            Assert.Equal("(0 (\"NEW\"))", value);
        }

        [Fact]
        public void AnAcceptActionThatDoesNotCallDoneDialog_KeepsTheDialogOpen()
        {
            // AutoCAD 2021 (GUI g1): an accept action without done_dialog keeps the dialog open, for a click as for Enter
            CadCommandSession session = With(out Shown host, "click accept", "enter", "esc");

            string value = Dialog(
                session,
                "ghoc",
                "(action_tile \"accept\" \"(setq A1 (if A1 (1+ A1) 1))\") (list (start_dialog) A1)"
            );

            Assert.Equal("(0 2)", value);
            Assert.DoesNotContain("script ended", host.Log);
        }

        [Fact]
        public void TheDefaultActionOfNewDialog_ServesOkAndCancelToo_OkStaysOpenAndEscCloses()
        {
            // AutoCAD 2021 (GUI g2): OK runs it and stays open, Esc runs it and closes with 0
            CadCommandSession session = With(out Shown host, "click b1", "click accept", "esc");

            string value = Eval(
                session,
                "(setq id (load_dialog \"@P\")) (new_dialog \"ghbtn\" id \"(setq DA (cons (list $key $reason) DA))\") (list (start_dialog) DA)"
            );

            Assert.Equal("(0 ((\"cancel\" 1) (\"accept\" 1) (\"b1\" 1)))", value);
        }

        [Fact]
        public void EnterInAnEditBoxWithAllowAccept_PressesTheDefaultTile_AndTheBoxGetsReasonTwoAfter()
        {
            // AutoCAD 2021 (GUI g1): accept saw the text, $key "accept" and $reason 1; the box's action came after with $reason 2
            CadCommandSession session = With(out Shown host, "type text \"NEW2\"", "enter");

            string value = Dialog(
                session,
                "ghedit",
                "(action_tile \"accept\" \"(setq R1 (list (get_tile \\\"text\\\") $key $reason)) (done_dialog 1)\") (action_tile \"text\" \"(setq TA (list $value $reason))\") (list (start_dialog) R1 TA)"
            );

            Assert.Equal("(1 (\"NEW2\" \"accept\" 1) (\"NEW2\" 2))", value);
        }

        [Fact]
        public void EnterInAnEditBoxWithoutAllowAccept_RunsItsActionWithReasonOne_AndTheDialogStaysOpen()
        {
            // AutoCAD 2021 (GUI g1): the action with $reason 1 and the dialog stays; $reason 2 as it closes
            CadCommandSession session = With(out Shown host, "type text \"NEW3\"", "enter");

            string value = Dialog(
                session,
                "ghplain",
                "(action_tile \"text\" \"(setq TA (cons (list $value $reason) TA))\") (list (start_dialog) TA)"
            );

            Assert.Equal("(0 ((\"NEW3\" 2) (\"NEW3\" 1)))", value);
            Assert.Contains("script ended", host.Log);
        }

        [Fact]
        public void TheEditBoxTypedIn_GetsReasonTwoAfterTheCancelActionWhenTheDialogCloses()
        {
            // AutoCAD 2021 (GUI g1) for the rule; ours: D340 (after the exit tile's action)
            CadCommandSession session = With(out Shown host, "type eb \"x\"", "esc");

            string value = Dialog(
                session,
                "ghoc",
                "(action_tile \"cancel\" \"(setq O (cons $key O))\") (action_tile \"eb\" \"(setq O (cons $key O))\") (list (start_dialog) O)"
            );

            Assert.Equal("(0 (\"eb\" \"cancel\"))", value);
        }

        [Fact]
        public void TheEditBoxTypedIn_GetsReasonTwoBeforeAClickOnAnotherTile()
        {
            // AutoCAD 2021 (GUI g3): an edit box left reports ("eb" "hey" 2) before the next tile's action
            CadCommandSession session = With(out Shown host, "type eb \"hey\"", "click bv", "esc");

            string value = Dialog(
                session,
                "ghvar",
                "(action_tile \"eb\" \"(setq O (cons (list $key $value $reason) O))\") (action_tile \"bv\" \"(setq O (cons $key O))\") (start_dialog) O"
            );

            Assert.Equal("(\"bv\" (\"eb\" \"hey\" 2))", value);
        }

        [Fact]
        public void EscWithNoCancelTile_EndsTheDialogWithZero()
        {
            // ours: D324 (a dialog with only an is_default button: no tile to press)
            WriteFile(
                "only.dcl",
                "only : dialog { : button { key = \"go\"; label = \"Go\"; is_default = true; action = \"(setq G 1)\"; } }"
            );
            CadCommandSession session = With(out Shown host, "esc");

            Assert.Equal(
                "(0 nil)",
                Eval(
                    session,
                    "(new_dialog \"only\" (load_dialog \""
                        + Slashed(Path.Combine(_folder, "only.dcl"))
                        + "\")) (list (start_dialog) G)"
                )
            );
        }

        [Fact]
        public void DoneDialog_GivesTheWindowsPosition_AndTheScreenPointGivenToNewDialog()
        {
            // AutoCAD 2021 (GUI g2): '(100 120) places the dialog there and done_dialog gives (100 120); the script host's own place is (0 0)
            CadCommandSession session = With(out Shown host, "click b1", "click b1");

            string value = Eval(
                session,
                "(setq id (load_dialog \"@P\")) (new_dialog \"ghbtn\" id) (action_tile \"b1\" \"(setq P1 (done_dialog))\") (setq a (start_dialog)) (new_dialog \"ghbtn\" id \"\" '(100 120)) (action_tile \"b1\" \"(setq P2 (done_dialog))\") (setq b (start_dialog)) (list a P1 b P2)"
            );

            Assert.Equal("(0 (0 0) 0 (100 120))", value);
        }

        [Fact]
        public void DoneDialogBeforeStartDialog_GivesZeroZeroAndDoesNothing()
        {
            // AutoCAD 2021 (GUI g1): (done_dialog 7) before start_dialog gives (0 0); start_dialog then shows the dialog
            CadCommandSession session = With(out Shown host, "click accept");

            Assert.Equal("((0 0) 1)", Dialog(session, "ghoc", "(list (done_dialog 7) (start_dialog))"));
        }

        [Fact]
        public void StartDialog_WithNoDialogMadeOrOneAlreadyShown_GivesZero()
        {
            // AutoCAD 2021 (d1): 0 with no dialog; ours: D317 (also inside the action of the dialog being shown)
            CadCommandSession session = With(out Shown host, "click b1");

            string value = Dialog(
                session,
                "ghbtn",
                "(action_tile \"b1\" \"(setq inner (start_dialog)) (done_dialog 3)\") (list (start_dialog) inner)"
            );

            Assert.Equal("(3 0)", value);
        }

        #endregion

        #region Actions and the $ variables

        [Fact]
        public void TheDollarVariables_AreBoundForTheActionAlone_AndGoneAfterIt()
        {
            // AutoCAD 2021 (GUI g3): button ("bv" "1" 1 data x y), toggle ("tg" "1" 1), edit box left ("eb" "hey" 2), radio
            // cluster ("rc" "r1" 1), radio button with its own action ("r2" "1" 1); unbound after
            CadCommandSession session = With(
                out Shown host,
                "click bv",
                "click tg",
                "type eb \"hey\"",
                "click r1",
                "click r2"
            );

            string value = Dialog(
                session,
                "ghvar",
                "(client_data_tile \"bv\" \"cdata\") (setq CX (/ (dimx_tile \"bv\") 2) CY (/ (dimy_tile \"bv\") 2)) "
                    + "(action_tile \"bv\" \"(setq KV (list $key $value $reason $data $x $y))\") "
                    + "(action_tile \"tg\" \"(setq TV (list $key $value $reason))\") "
                    + "(action_tile \"eb\" \"(setq EV (cons (list $key $value $reason) EV))\") "
                    + "(action_tile \"rc\" \"(setq RV (cons (list $key $value $reason) RV))\") "
                    + "(action_tile \"r2\" \"(setq R2V (list $key $value $reason))\") "
                    + "(setq s (start_dialog)) "
                    + "(list s (list (nth 0 KV) (nth 1 KV) (nth 2 KV) (nth 3 KV) (= (nth 4 KV) CX) (= (nth 5 KV) CY)) TV EV RV R2V (boundp '$key) (boundp '$value) (boundp '$reason) (boundp '$data) (boundp '$x) (boundp '$y))"
            );

            Assert.Equal(
                "(0 (\"bv\" \"1\" 1 \"cdata\" T T) (\"tg\" \"1\" 1) ((\"eb\" \"hey\" 2)) ((\"rc\" \"r1\" 1)) (\"r2\" \"1\" 1) nil nil nil nil nil nil)",
                value
            );
        }

        [Fact]
        public void AButtonClickedAtAPoint_GivesItsActionThePointInDollarXAndDollarY()
        {
            // AutoCAD 2021 (GUI g3): $x $y are the click point in the button; ours: D329 (the script gives the point)
            CadCommandSession session = With(out Shown host, "click ib 10 20", "dblclick ib 3 4");

            string value = Dialog(
                session,
                "ghall",
                "(action_tile \"ib\" \"(setq O (cons (list $value $reason $x $y) O))\") (start_dialog) O"
            );

            Assert.Equal("((\"1\" 4 3 4) (\"1\" 1 10 20))", value);
        }

        [Fact]
        public void TheVariablesOfARoutine_SurviveAnAction_BecauseTheyAreGivenBackAsTheyWere()
        {
            // ours: D328 (a variable the action's names hold is restored when it ends)
            CadCommandSession session = With(out Shown host, "click b1");

            string value = Dialog(
                session,
                "ghbtn",
                "(setq $key \"mine\") (action_tile \"b1\" \"(setq inside $key)\") (start_dialog) (list inside $key)"
            );

            Assert.Equal("(\"b1\" \"mine\")", value);
        }

        [Fact]
        public void AnActionSeesTheRoutinesLocals_AsADeeperCallDoes()
        {
            // ours: D323 (dynamic scoping: the action runs in the environment of the routine that called start_dialog)
            CadCommandSession session = With(out Shown host, "click b1");

            string value = Dialog(
                session,
                "ghbtn",
                "(defun run (/ old) (setq old 5) (action_tile \"b1\" \"(setq old (1+ old))\") (start_dialog) old) (run)"
            );

            Assert.Equal("6", value);
        }

        [Fact]
        public void AnActionOfTheDclAttribute_RunsWhereActionTileGaveNone_AndActionTileOverridesIt()
        {
            // reference: a tile's action is its DCL action attribute unless action_tile gave another (D323)
            WriteFile(
                "act.dcl",
                "act : dialog { : button { key = \"a\"; label = \"A\"; action = \"(setq W (cons 'dcl W))\"; } : button { key = \"b\"; label = \"B\"; action = \"(setq W (cons 'dcl W))\"; } ok_cancel; }"
            );
            CadCommandSession session = With(out Shown host, "click a", "click b");

            string value = Eval(
                session,
                "(new_dialog \"act\" (load_dialog \""
                    + Slashed(Path.Combine(_folder, "act.dcl"))
                    + "\")) (action_tile \"b\" \"(setq W (cons 'tile W))\") (start_dialog) W"
            );

            Assert.Equal("(TILE DCL)", value);
        }

        [Fact]
        public void ActionTile_GivesT_AlsoForAKeyNoTileHas_AndAnEmptyStringTakesTheActionAway()
        {
            // AutoCAD 2021 (GUI g1): T for a missing key; ours: D323 ("" is none)
            CadCommandSession session = With(out Shown host, "click b1");

            string value = Dialog(
                session,
                "ghbtn",
                "(setq a (list (action_tile \"b1\" \"(setq X 1)\") (action_tile \"nokey\" \"(setq X 2)\"))) (action_tile \"b1\" \"\") (setq s (start_dialog)) (list a s X)"
            );

            Assert.Equal("((T T) 0 nil)", value);
        }

        [Fact]
        public void ClientDataTile_GivesTheData_AndNilForAMissingKey()
        {
            // AutoCAD 2021 (GUI g1)
            CadCommandSession session = With(out Shown host);

            Assert.Equal(
                "(\"cd\" nil)",
                Dialog(session, "ghall", "(list (client_data_tile \"btn\" \"cd\") (client_data_tile \"nokey\" \"cd\"))")
            );
        }

        [Fact]
        public void ModeTile_DisablesAndEnables_AndADisabledTileRefusesTheClick()
        {
            // reference: mode 1 disables, mode 0 enables; a disabled tile takes no input (the script logs refused)
            CadCommandSession session = With(out Shown host, "click b2", "click b1", "click b1", "esc");

            string value = Dialog(
                session,
                "ghbtn",
                "(action_tile \"b1\" \"(setq N (if N (1+ N) 1)) (mode_tile \\\"b1\\\" 1) (mode_tile \\\"b2\\\" 0)\") (action_tile \"b2\" \"(setq B2 T)\") (mode_tile \"nokey\" 1) (list (start_dialog) N B2)"
            );

            Assert.Equal("(0 1 nil)", value);
            Assert.Equal(
                new[] { "refused click b2", "refused click b1" },
                host.Log.Where(l => l.StartsWith("refused", StringComparison.Ordinal)).ToArray()
            );
        }

        [Fact]
        public void ModeTile_Two_Three_AndFour_MoveTheFocus_SelectTheTextAndFlipTheHighlight()
        {
            // reference: 2 gives the focus, 3 selects an edit box's text, 4 flips an image button's highlight
            CadCommandSession session = With(out Shown host, "esc");

            string value = Dialog(
                session,
                "ghall",
                "(list (mode_tile \"txd\" 2) (mode_tile \"txt\" 3) (mode_tile \"ib\" 4) (mode_tile \"ib\" 9) (start_dialog))"
            );

            Assert.Equal("(nil nil nil nil 0)", value);
            DclDialog dialog = host.Dialogs.Single();
            Assert.Equal("txd", dialog.FocusKey);
            Assert.Equal("txt", dialog.SelectTextKey);
            Assert.True(dialog.Find("ib").IsFlipped);
        }

        [Fact]
        public void TheScriptsKindsOfInput_GiveTheValuesAndReasonsOfTheirTiles()
        {
            // reference: reasons 1 picked, 3 slider dragged, 4 double click; a slider is cut to its limits
            CadCommandSession session = With(
                out Shown host,
                "select lst 0 2",
                "dblclick lst 1",
                "slide sl 500",
                "slide sl 30 drag",
                "select pop 2",
                "click tg0",
                "click tg0",
                "esc"
            );

            string value = Dialog(
                session,
                "ghall",
                "(foreach k '(\"lst\" \"sl\" \"pop\" \"tg0\") (action_tile k \"(setq O (cons (list $key $value $reason) O))\")) (start_dialog) (reverse O)"
            );

            Assert.Equal(
                "((\"lst\" \"0 2\" 1) (\"lst\" \"1\" 4) (\"sl\" \"100\" 1) (\"sl\" \"30\" 3) (\"pop\" \"2\" 1) (\"tg0\" \"1\" 1) (\"tg0\" \"0\" 1))",
                value
            );
        }

        [Fact]
        public void AnEditBoxTypedInBeyondItsLimit_IsCutAndTheRoutineReadsTheCutText()
        {
            // reference: edit_limit is the number of characters typing allows
            CadCommandSession session = With(out Shown host, "type e \"abcdefgh\"", "click accept");

            string value = Dialog(
                session,
                "ghlimit",
                "(action_tile \"accept\" \"(setq V (get_tile \\\"e\\\")) (done_dialog 1)\") (list (start_dialog) V)"
            );

            Assert.Equal("(1 \"abcde\")", value);
        }

        #endregion

        #region Nesting, term_dialog, hide and pick

        [Fact]
        public void ADialogMadeAndStartedInAnAction_OpensOverItsParentAndEndsBeforeIt()
        {
            // reference: a child dialog takes the input until it ends; the parent stays shown
            CadCommandSession session = With(out Shown host, "click b1", "click accept", "click accept");

            string value = Dialog(
                session,
                "ghbtn",
                "(action_tile \"b1\" \"(new_dialog \\\"ghoc\\\" id) (setq CS (start_dialog))\") (list (start_dialog) CS)"
            );

            Assert.Equal("(1 1)", value);
            Assert.Equal(host.Dialogs[0], host.Dialogs[1].Parent);
            Assert.Null(host.Dialogs[0].Parent);
            Assert.Equal(
                new[] { "open ghbtn", "open ghoc", "close ghoc 1", "close ghbtn 1" },
                host.Log.Where(l =>
                        l.StartsWith("open", StringComparison.Ordinal)
                        || l.StartsWith("close", StringComparison.Ordinal)
                    )
                    .ToArray()
            );
        }

        [Fact]
        public void TermDialogInAChild_MakesBothStartDialogsGiveMinusOne()
        {
            // AutoCAD 2021 (GUI g3): the child's and the parent's start_dialog give -1, term_dialog gives nil
            CadCommandSession session = With(out Shown host, "click b1", "click b1");

            string value = Dialog(
                session,
                "ghbtn",
                "(action_tile \"b1\" \"(new_dialog \\\"ghbtn\\\" id) (action_tile \\\"b1\\\" \\\"(setq TT (term_dialog))\\\") (setq CS (start_dialog))\") (list (start_dialog) CS TT)"
            );

            Assert.Equal("(-1 -1 nil)", value);
        }

        [Fact]
        public void TermDialogWhenTheEditBoxWasTypedIn_RunsNoMoreActions()
        {
            // ours: D325 (a terminated dialog runs nothing after the action that ended it)
            CadCommandSession session = With(out Shown host, "type eb \"x\"", "esc");

            string value = Dialog(
                session,
                "ghvar",
                "(action_tile \"cancel\" \"(term_dialog)\") (action_tile \"eb\" \"(setq LATE 1)\") (list (start_dialog) LATE)"
            );

            Assert.Equal("(-1 nil)", value);
        }

        [Fact]
        public void HideAndPick_TheActionEndsTheDialogWith2_TheRoutineAsksForAPoint_AndMakesTheDialogAgain()
        {
            // reference: done_dialog 2, start_dialog gives 2, the routine calls getpoint and starts the dialog again
            CadCommandSession session = With(out Shown host, "click b1", "esc");
            Run(
                session,
                "(defun hp (/ s1 p s2) (setq id (load_dialog \""
                    + Slashed(_path)
                    + "\")) (new_dialog \"ghbtn\" id) (action_tile \"b1\" \"(done_dialog 2)\") (setq s1 (start_dialog)) (if (= s1 2) (setq p (getpoint \"Pick: \"))) (new_dialog \"ghbtn\" id) (setq s2 (start_dialog)) (list s1 p s2))"
            );

            Run(session, "(setq r (hp))");

            Assert.True(session.IsRunning);
            Assert.Equal("Pick: ", session.PromptText);

            Run(session, "5,6");

            Assert.Equal("(2 (5.0 6.0 0.0) 0)", Eval(session, "r"));
            Assert.Equal(new[] { "ghbtn", "ghbtn" }, host.Dialogs.Select(d => d.Name).ToArray());
        }

        #endregion

        #region Errors and input in an action

        [Fact]
        public void AnErrorInAnAction_EndsEveryDialogAndTheEvaluation_NotCaughtByVlCatchAllApply()
        {
            // AutoCAD 2021 (GUI g2): (car 5) in an action ends the whole evaluation, vl-catch-all-apply does not catch it;
            // ours: D343 (written as ; error:, *error* not run)
            CadCommandSession session = With(out Shown host, "click accept");

            LispResult result = session.Lisp.Run(
                "(setq *error* (lambda (m) (setq RAN m))) (setq id (load_dialog \""
                    + Slashed(_path)
                    + "\")) (new_dialog \"ghoc\" id) (action_tile \"accept\" \"(car 5)\") (setq R (vl-catch-all-apply 'start_dialog nil)) (setq AFTER 1)"
            );

            Assert.True(result.Failed);
            Assert.Equal("bad argument type: consp 5", result.Error);
            Assert.Equal("; error: bad argument type: consp 5", result.Output);
            Assert.Null(session.Lisp.GetValue("AFTER"));
            Assert.Null(session.Lisp.GetValue("R"));
            Assert.Null(session.Lisp.GetValue("RAN"));
            Assert.Contains("close ghoc 0", host.Log);
            Assert.Equal(0, session.Lisp.RunningDialogs);
        }

        [Fact]
        public void AfterAnErrorInAnAction_TheDollarVariablesAreUnbound_AndTheNextDialogWorks()
        {
            // ours: D343 (the variables are given back; the interpreter is as before)
            CadCommandSession session = With(out Shown host, "click accept", "click accept");
            session.Lisp.Run(
                "(setq id (load_dialog \""
                    + Slashed(_path)
                    + "\")) (new_dialog \"ghoc\" id) (action_tile \"accept\" \"(car 5)\") (start_dialog)"
            );

            Assert.Equal("(nil nil)", Eval(session, "(list (boundp '$key) (boundp '$reason))"));
            Assert.Equal("1", Eval(session, "(new_dialog \"ghoc\" id) (start_dialog)"));
        }

        [Fact]
        public void AnErrorInAChildsAction_EndsTheParentToo()
        {
            // AutoCAD 2021 (GUI g2): every dialog ends
            CadCommandSession session = With(out Shown host, "click b1", "click accept");

            LispResult result = session.Lisp.Run(
                "(setq id (load_dialog \""
                    + Slashed(_path)
                    + "\")) (new_dialog \"ghbtn\" id) (action_tile \"b1\" \"(new_dialog \\\"ghoc\\\" id) (action_tile \\\"accept\\\" \\\"(car 5)\\\") (start_dialog)\") (start_dialog)"
            );

            Assert.True(result.Failed);
            Assert.Equal(
                new[] { "close ghoc 0", "close ghbtn 0" },
                host.Log.Where(l => l.StartsWith("close", StringComparison.Ordinal)).ToArray()
            );
        }

        [Theory]
        [InlineData("(command \"LINE\")")]
        [InlineData("(vl-cmdf \"LINE\")")]
        [InlineData("(getpoint)")]
        [InlineData("(getstring \"x\")")]
        [InlineData("(entsel)")]
        [InlineData("(ssget)")]
        public void CommandAndTheGetFunctions_InAnAction_EndTheEvaluationWithAutoCadRejectedFunction(string call)
        {
            // AutoCAD 2021 (GUI g3): command in an action ends the evaluation, not caught by vl-catch-all-apply; ours: D327,
            // D343 (every wait is refused alike; getpoint is not measured)
            CadCommandSession session = With(out Shown host, "click accept");

            LispResult result = session.Lisp.Run(
                "(setq id (load_dialog \""
                    + Slashed(_path)
                    + "\")) (new_dialog \"ghoc\" id) (action_tile \"accept\" \"(vl-catch-all-apply '(lambda () "
                    + call.Replace("\"", "\\\"")
                    + " nil) nil) (setq PASSED 1)\") (start_dialog) (setq AFTER 1)"
            );

            Assert.True(result.Failed);
            Assert.Equal("AutoCAD rejected function", result.Error);
            Assert.Equal("; error: AutoCAD rejected function", result.Output);
            Assert.Null(session.Lisp.GetValue("PASSED"));
            Assert.Null(session.Lisp.GetValue("AFTER"));
            Assert.Equal(0, session.Lisp.RunningDialogs);
            Assert.False(session.IsLispWaiting);
        }

        [Fact]
        public void ASessionThatWasRefusedInADialog_AsksTheUserAgainAfterwards()
        {
            // ours: D327 (the refusal is only while a dialog is shown)
            CadCommandSession session = With(out Shown host, "click accept");
            session.Lisp.Run(
                "(setq id (load_dialog \""
                    + Slashed(_path)
                    + "\")) (new_dialog \"ghoc\" id) (action_tile \"accept\" \"(getpoint)\") (start_dialog)"
            );

            Run(session, "(getpoint \"Again: \")");

            Assert.Equal("Again: ", session.PromptText);
        }

        [Fact]
        public void ADoubleClickInAListWithAllowAccept_RunsTheActionWithReasonFour_ThenPressesTheDefaultTile()
        {
            // reference: a double click runs the action with $reason 4 and, with allow_accept, presses the default tile
            CadCommandSession session = With(out Shown host, "dblclick lst 1");

            string value = Dialog(
                session,
                "ghlist",
                "(action_tile \"lst\" \"(setq O (cons (list $value $reason) O))\") (action_tile \"accept\" \"(setq O (cons $key O)) (done_dialog 1)\") (list (start_dialog) O)"
            );

            Assert.Equal("(1 (\"accept\" (\"1\" 4)))", value);
        }

        [Fact]
        public void EnterOnAButton_PressesThatButton_AndEnterWithNoFocusPressesTheDefaultTile()
        {
            // reference: Enter presses the focused button, else the default tile; ours: D329 (the key names the focus)
            CadCommandSession session = With(out Shown host, "enter cancel", "enter");

            Assert.Equal(
                "(0 1)",
                Dialog(session, "ghoc", "(setq a (start_dialog)) (new_dialog \"ghoc\" id) (list a (start_dialog))")
            );
        }

        [Fact]
        public void ADialogGivenByTheLibraryDirectly_RunsLikeOneLoadedFromAFile()
        {
            // ours: D315 (a test adds a file already parsed; the ids go on from 1)
            CadCommandSession session = With(out Shown host, "click accept");
            int id = session.DclLibrary.Add(
                DclParser.Parse("hand : dialog { label = \"H\"; ok_only; }", @"C:\hand.dcl", null)
            );

            Assert.Equal(1, id);
            Assert.Equal("(T 1)", Eval(session, "(list (new_dialog \"hand\" 1) (start_dialog))"));
        }

        [Fact]
        public void TheScreenPointOfNewDialog_CentresAlongAnAxisWhereItIsBelowZero()
        {
            // reference: '(-1 -1) centres the dialog; ours: D316 (each coordinate on its own)
            CadCommandSession session = With(out Shown host, "esc", "esc", "esc");

            Dialog(
                session,
                "ghoc",
                "(start_dialog) (new_dialog \"ghoc\" id \"\" '(-1 -1)) (start_dialog) (new_dialog \"ghoc\" id \"\" '(100 -1)) (start_dialog)"
            );

            Assert.Equal(new int?[] { null, null, 100 }, host.Dialogs.Select(d => d.ScreenX).ToArray());
            Assert.Equal(new int?[] { null, null, null }, host.Dialogs.Select(d => d.ScreenY).ToArray());
        }

        [Fact]
        public void AlertInAnAction_StillWorks()
        {
            // reference: alert and getfiled are dialogs of their own; ours: D327
            CadCommandSession session = With(out Shown host, "click accept");

            string value = Dialog(
                session,
                "ghoc",
                "(action_tile \"accept\" \"(alert \\\"hello\\\") (done_dialog 4)\") (start_dialog)"
            );

            Assert.Equal("4", value);
            Assert.Contains("hello", session.History);
        }

        #endregion

        #region The script host

        [Fact]
        public void WhenTheStepsRunOut_TheHostPressesEscAndLogsScriptEnded()
        {
            // ours: D329
            CadCommandSession session = With(out Shown host);

            Assert.Equal("0", Dialog(session, "ghoc", "(start_dialog)"));
            Assert.Equal(
                new[] { "open ghoc", "dump ghoc eb=\"abc\" accept=nil cancel=nil", "script ended", "close ghoc 0" },
                host.Log
            );
        }

        [Fact]
        public void AStepForATileThatIsNotThereIsDisabledOrOfAnotherKind_IsLoggedAsRefusedAndSkipped()
        {
            // ours: D329
            CadCommandSession session = With(
                out Shown host,
                "click nokey",
                "click b2",
                "type b1 \"x\"",
                "select b1 0",
                "slide b1 5",
                "frobnicate",
                "type b1 \"unclosed",
                "click accept"
            );

            Assert.Equal("1", Dialog(session, "ghbtn", "(start_dialog)"));
            Assert.Equal(
                new[]
                {
                    "refused click nokey",
                    "refused click b2",
                    "refused type b1 \"x\"",
                    "refused select b1 0",
                    "refused slide b1 5",
                    "refused frobnicate",
                    "refused type b1 \"unclosed",
                },
                host.Log.Where(l => l.StartsWith("refused", StringComparison.Ordinal)).ToArray()
            );
        }

        [Fact]
        public void TheDumpStep_WritesEveryKeyedTileInOrderWithItsValueAsPrin1Writes()
        {
            // ours: D329
            CadCommandSession session = With(out Shown host, "type eb \"a \\\"b\\\"\"", "dump", "esc");

            Dialog(session, "ghoc", "(start_dialog)");

            Assert.Equal("dump ghoc eb=\"a \\\"b\\\"\" accept=nil cancel=nil", host.Log[2]);
        }

        [Fact]
        public void ADialogThatIgnoresEsc_IsEndedByTheHostAtTheNextCallWithZero()
        {
            // ours: D329 (a script must not hang a test)
            CadCommandSession session = With(out Shown host);

            // The OK of ok_only is what Esc presses, and its action leaves the dialog open.
            string value = Dialog(session, "ghok", "(action_tile \"accept\" \"(setq A 1)\") (list (start_dialog) A)");

            Assert.Equal("(0 1)", value);
            Assert.Equal(new[] { "script ended", "close ghok 0" }, host.Log.Skip(2).ToArray());
        }

        [Fact]
        public void ASelectOfSeveralItemsInASingleList_IsRefused()
        {
            // ours: D329
            CadCommandSession session = With(out Shown host, "select lst1 0 1", "select lst 0 1", "select lst 9");

            Dialog(session, "ghall", "(start_dialog)");

            Assert.Equal(
                new[] { "refused select lst1 0 1", "refused select lst 9" },
                host.Log.Where(l => l.StartsWith("refused", StringComparison.Ordinal)).ToArray()
            );
        }

        #endregion
    }
}
