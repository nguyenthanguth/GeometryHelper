using System;
using System.Linq;
using GeometryHelper.Cad.Lisp;
using Xunit;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The functions of AutoCAD the presenter has no use for or cannot have, against AutoCAD 2021's accoreconsole (probes g7,
    /// g8, g12 and g15 of the C4a measurements): <c>gc</c>, <c>mem</c>, <c>alloc</c>, <c>expand</c>, <c>pragma</c>, <c>arx</c> and
    /// <c>ads</c>; the refused ones of ObjectARX, Visual LISP and the registry (decision D1219); and the old names of the
    /// variant and safearray functions.
    /// </summary>
    [Collection("LispSerial")]
    public class LispAutoCadOnlyTests
    {
        private static LispInterpreter Loaded()
        {
            var lisp = new LispInterpreter();
            lisp.Run("(vl-load-com)");
            return lisp;
        }

        private static string Out(string expression) => Loaded().Run(expression).Output;

        #region The no-ops

        [Theory]
        [InlineData("(gc)", "nil")] // AutoCAD 2021 (g7): nil
        [InlineData("(gc 1)", "; error: too many arguments")] // AutoCAD 2021 (g7)
        [InlineData("(alloc 100)", "256")] // AutoCAD 2021 (g15): the size it had
        [InlineData("(alloc)", "; error: too few arguments")] // AutoCAD 2021 (g15)
        [InlineData("(alloc \"a\")", "; error: bad argument type: fixnump: \"a\"")] // AutoCAD 2021 (g15)
        [InlineData("(alloc 1.5)", "; error: bad argument type: fixnump: 1.5")] // AutoCAD 2021 (g15)
        [InlineData("(expand 0)", "0")] // AutoCAD 2021 (g15): 0
        [InlineData("(expand -1)", "-1")] // AutoCAD 2021 (g15): -1
        [InlineData("(expand \"a\")", "; error: bad argument type: fixnump: \"a\"")] // AutoCAD 2021 (g7)
        [InlineData("(expand 1 2)", "; error: too many arguments")] // AutoCAD 2021 (g7)
        [InlineData("(pragma '(a))", "nil")] // AutoCAD 2021 (g15): nil
        [InlineData("(pragma '(a b))", "nil")] // AutoCAD 2021 (g7): nil
        [InlineData("(pragma 'a)", "; error: bad argument type: consp A")] // AutoCAD 2021 (g7)
        [InlineData("(pragma \"a\")", "; error: bad argument type: consp \"a\"")] // AutoCAD 2021 (g15)
        [InlineData("(pragma nil)", "; error: bad argument type: symbolp nil")] // AutoCAD 2021 (g15)
        [InlineData("(pragma)", "; error: bad argument type: symbolp nil")] // AutoCAD 2021 (g7)
        [InlineData("(pragma '(a) 1)", "; error: syntax error")] // AutoCAD 2021 (g15)
        [InlineData("(mem 1)", "; error: too many arguments")] // AutoCAD 2021: (mem) takes none
        public void TheNoOps_AnswerAsAutoCadDoes(string expression, string output) =>
            Assert.Equal(output, Out(expression));

        [Fact]
        public void AllocGivesTheSizeItHadBefore()
        {
            // AutoCAD 2021 (g15): (alloc 100) 256, (alloc 100) 100, (alloc 300) 100, (alloc 0) 300, (alloc -5) 0
            LispInterpreter lisp = Loaded();

            Assert.Equal("256", lisp.Run("(alloc 100)").Output);
            Assert.Equal("100", lisp.Run("(alloc 100)").Output);
            Assert.Equal("100", lisp.Run("(alloc 300)").Output);
            Assert.Equal("300", lisp.Run("(alloc 0)").Output);
            Assert.Equal("0", lisp.Run("(alloc -5)").Output);
        }

        [Fact]
        public void MemWritesTheShapeOfAutoCadsReport_AndGivesNil()
        {
            // AutoCAD 2021 (g7, g15): the GC line, a quoted heading, the table of segments, the segment size, then nil.
            LispResult result = Loaded().Run("(mem)");
            string[] lines = result.Output.Split('\n');

            Assert.StartsWith("\"; GC calls: \"", lines[0]);
            Assert.EndsWith("\"; GC run time: \"0\" ms\"", lines[0]);
            Assert.Contains("PgSz  Used  Free  FMCL  Segs  Type", lines[1]);
            Assert.Contains(lines, l => l.EndsWith("CONS memory", StringComparison.Ordinal));
            Assert.StartsWith("\"Segment size: \"65536\"", lines[lines.Length - 2]);
            Assert.Equal("nil", lines[lines.Length - 1]);
            Assert.Null(result.Value);
        }

        [Fact]
        public void GcCountsTheCollectionsMemReports()
        {
            LispInterpreter lisp = Loaded();
            string before = lisp.Run("(mem)").Output.Split('\n')[0];
            lisp.Run("(gc)");
            string after = lisp.Run("(mem)").Output.Split('\n')[0];

            Assert.NotEqual(before, after);
        }

        [Fact]
        public void NothingIsLoaded_SoArxAndAdsGiveNil()
        {
            // AutoCAD 2021 (g7): (arx) is the list of the ObjectARX applications it has loaded; here there are none. (ads) is an
            // error there for a function the console lacks (ads-loaded); nil is the list of none.
            Assert.Equal("nil", Out("(arx)"));
            Assert.Equal("nil", Out("(ads)"));
            Assert.Equal("; error: too many arguments", Out("(arx 1)"));
            Assert.Equal("; error: too many arguments", Out("(ads 1)"));
        }

        #endregion

        #region The refused ones

        [Theory]
        [InlineData("(arxload \"geomcal\")", "ARXLOAD is not available: ObjectARX applications load only in AutoCAD.")]
        [InlineData(
            "(arxunload \"geomcal\")",
            "ARXUNLOAD is not available: ObjectARX applications load only in AutoCAD."
        )]
        [InlineData(
            "(vlisp-compile 'st \"a.lsp\")",
            "VLISP-COMPILE is not available: Visual LISP compiles and makes projects only in AutoCAD."
        )]
        [InlineData(
            "(vlisp-load-project \"p.prj\")",
            "VLISP-LOAD-PROJECT is not available: Visual LISP compiles and makes projects only in AutoCAD."
        )]
        [InlineData(
            "(vlax-add-cmd \"A\" 'f)",
            "VLAX-ADD-CMD is not available: AutoCAD's command table is not here to add a command to."
        )]
        [InlineData(
            "(vlax-remove-cmd \"A\")",
            "VLAX-REMOVE-CMD is not available: AutoCAD's command table is not here to take a command from."
        )]
        [InlineData(
            "(vlax-reg-app \"x\")",
            "VLAX-REG-APP is not available: AutoCAD's ActiveX server is not here to register an application with."
        )]
        [InlineData(
            "(vlax-product-key)",
            "VLAX-PRODUCT-KEY is not available: AutoCAD's product keys are not kept here."
        )]
        [InlineData(
            "(vlax-machine-product-key)",
            "VLAX-MACHINE-PRODUCT-KEY is not available: AutoCAD's product keys are not kept here."
        )]
        [InlineData(
            "(vlax-user-product-key)",
            "VLAX-USER-PRODUCT-KEY is not available: AutoCAD's product keys are not kept here."
        )]
        [InlineData("(vl-init)", "VL-INIT is not available: the Visual LISP runtime belongs to AutoCAD.")]
        [InlineData("(vlarts-init)", "VLARTS-INIT is not available: the Visual LISP runtime belongs to AutoCAD.")]
        [InlineData("(vl-bt)", "VL-BT is not available: the backtrace needs AutoCAD's Visual LISP debugger.")]
        [InlineData("(vl-bt-on)", "VL-BT-ON is not available: the backtrace needs AutoCAD's Visual LISP debugger.")]
        [InlineData("(vl-bt-off)", "VL-BT-OFF is not available: the backtrace needs AutoCAD's Visual LISP debugger.")]
        [InlineData("(c:vlide)", "C:VLIDE is not available: the Visual LISP editor is part of AutoCAD only.")]
        [InlineData("(c:vldebug)", "C:VLDEBUG is not available: the Visual LISP debugger is part of AutoCAD only.")]
        public void AFunctionOfAutoCadAlone_IsAnErrorThatNamesItAndWhy(string expression, string message)
        {
            LispInterpreter lisp = Loaded();

            Assert.Equal("; error: " + message, lisp.Run(expression).Output);
            Assert.Equal(
                "\"" + message.Replace("\"", "\\\"") + "\"",
                lisp.Run(
                    "(vl-catch-all-error-message (vl-catch-all-apply (function (lambda () " + expression + ")) nil))"
                ).Output
            );
        }

        [Fact]
        public void TheTwentyVisualLispProjectFunctions_AreAllDefinedAndRefused()
        {
            string[] names =
            {
                "vlisp-compile",
                "vlisp-compile-list",
                "vlisp-dclres-list",
                "vlisp-dclres-load-dialog",
                "vlisp-drop-project",
                "vlisp-export-symbol",
                "vlisp-fasres-list",
                "vlisp-fasres-load",
                "vlisp-find-project",
                "vlisp-import-exsubrs",
                "vlisp-import-symbol",
                "vlisp-inires-list",
                "vlisp-list-loaded-projects",
                "vlisp-load-project",
                "vlisp-make-file-fas",
                "vlisp-make-project-fas",
                "vlisp-make-project-load-file",
                "vlisp-project-fas-list",
                "vlisp-read-file-xdv-list",
                "vlisp-read-project-xdv-list",
            };
            LispInterpreter lisp = Loaded();

            Assert.Equal(20, names.Length);

            foreach (string name in names)
            {
                Assert.Equal("SUBR", lisp.Run("(type " + name + ")").Output);
                Assert.StartsWith(
                    "; error: " + name.ToUpperInvariant() + " is not available: ",
                    lisp.Run("(" + name + ")").Output
                );
            }
        }

        [Theory]
        [InlineData("(arxload \"zzz\" 1)", "1")] // AutoCAD 2021 (g7): the value for a failed load is given back
        [InlineData("(arxload \"zzz\" nil)", "nil")] // AutoCAD 2021 (g7)
        [InlineData("(arxunload \"zzz\" nil)", "nil")] // AutoCAD 2021 (g7)
        [InlineData("(arxunload \"zzz\" 1)", "1")] // AutoCAD 2021 (g7)
        [InlineData("(arxload 5)", "; error: bad argument type: stringp 5")] // AutoCAD 2021 (g7)
        [InlineData("(arxload)", "; error: too few arguments")] // AutoCAD 2021 (g7)
        [InlineData("(arxload \"zzz\" \"a\" 3)", "; error: too many arguments")] // AutoCAD 2021 (g7)
        [InlineData("(arxunload)", "; error: too few arguments")] // AutoCAD 2021 (g7)
        public void ArxloadAndArxunload_KeepAutoCadsArgumentsAndItsValueForAFailedLoad(
            string expression,
            string output
        ) => Assert.Equal(output, Out(expression));

        [Fact]
        public void WhatExistsBeforeVlLoadCom_IsAsInAutoCad()
        {
            // AutoCAD 2021 (g8): all but the safearray and variant names, vlax-dump-object, vlax-create-object and the registration
            // and product keys of ActiveX exist before (vl-load-com); vlax-add-cmd and vlax-remove-cmd do.
            var lisp = new LispInterpreter();

            foreach (
                string name in new[]
                {
                    "vlax-add-cmd",
                    "vlax-remove-cmd",
                    "vl-init",
                    "vl-bt",
                    "c:vlide",
                    "arx",
                    "gc",
                    "getenv",
                    "snvalid",
                    "autoload",
                }
            )
            {
                Assert.Equal("SUBR", lisp.Run("(type " + name + ")").Output);
            }

            foreach (
                string name in new[]
                {
                    "vlax-reg-app",
                    "vlax-product-key",
                    "vlax-machine-product-key",
                    "vlax-user-product-key",
                    "safearray",
                    "variant",
                    "safearray-type",
                    "variant-type",
                }
            )
            {
                Assert.Equal("nil", lisp.Run("(type " + name + ")").Output);
            }

            lisp.Run("(vl-load-com)");

            foreach (
                string name in new[]
                {
                    "vlax-reg-app",
                    "vlax-product-key",
                    "vlax-machine-product-key",
                    "vlax-user-product-key",
                    "safearray",
                    "variant",
                    "safearray-type",
                    "variant-type",
                }
            )
            {
                Assert.Equal("SUBR", lisp.Run("(type " + name + ")").Output);
            }
        }

        #endregion

        #region The old names of the variant and safearray functions

        [Theory]
        [InlineData("(type (safearray vlax-vbDouble '(0 . 2)))", "safearray")] // AutoCAD 2021 (g12)
        [InlineData("(safearray vlax-vbDouble '(0 . 2))", "#<safearray...>")] // AutoCAD 2021 (g12)
        [InlineData("(safearray-get-dim (safearray vlax-vbDouble '(0 . 1) '(0 . 2)))", "2")] // AutoCAD 2021 (g12)
        [InlineData("(safearray-get-l-bound (safearray vlax-vbDouble '(0 . 2)) 1)", "0")] // AutoCAD 2021 (g12)
        [InlineData("(safearray-get-u-bound (safearray vlax-vbDouble '(0 . 1) '(0 . 2)) 2)", "2")] // AutoCAD 2021 (g12)
        [InlineData(
            "(safearray-get-l-bound (safearray vlax-vbDouble '(0 . 1) '(0 . 2)) 3)",
            "; error: ActiveX Server returned an error: Invalid index"
        )] // AutoCAD 2021 (g12)
        [InlineData("(safearray-get-dim 5)", "; error: bad argument type: safearrayp 5")] // AutoCAD 2021 (g12)
        [InlineData("(safearray-type (safearray vlax-vbDouble '(0 . 2)))", "5")] // AutoCAD 2021 (g12)
        [InlineData("(safearray-type 5)", "; error: bad argument type: safearrayp 5")] // AutoCAD 2021 (g12)
        [InlineData(
            "(progn (setq b (safearray vlax-vbDouble '(0 . 2))) (safearray-put-element b 0 1.5) (safearray-get-element b 0))",
            "1.5"
        )] // AutoCAD 2021 (g12)
        [InlineData(
            "(progn (setq b (safearray vlax-vbDouble '(0 . 2))) (safearray-put-element b 5 1.5))",
            "; error: ActiveX Server returned an error: Invalid index"
        )] // AutoCAD 2021 (g12)
        [InlineData("(safearray vlax-vbDouble 5)", "; error: Array specification (bounds) is not a dotted pair.")] // AutoCAD 2021 (g12)
        [InlineData("(safearray)", "; error: too few arguments")] // AutoCAD 2021 (g12)
        [InlineData("(safearray-get-element (safearray vlax-vbDouble '(0 . 2)))", "; error: too few arguments")] // AutoCAD 2021 (g12)
        [InlineData("(variant 5)", "#<variant 3 5>")] // AutoCAD 2021 (g12)
        [InlineData("(variant)", "#<variant 0 >")] // AutoCAD 2021 (g12)
        [InlineData("(variant vlax-vbInteger 5)", "#<variant 5 2>")] // AutoCAD 2021 (g12): the value first, then the type
        [InlineData("(variant vlax-vbString)", "#<variant 3 8>")] // AutoCAD 2021 (g12)
        [InlineData("(type (variant 5))", "variant")] // AutoCAD 2021 (g12)
        [InlineData("(variant-type (variant vlax-vbInteger 5))", "5")] // AutoCAD 2021 (g12)
        [InlineData("(variant-type 5)", "; error: bad argument type: variantp 5")] // AutoCAD 2021 (g12)
        [InlineData("(variant vlax-vbInteger \"a\")", "; error: bad argument type: fixnump: \"a\"")] // AutoCAD 2021 (g12)
        [InlineData("(variant 8 \"abc\" 1)", "; error: too many arguments")] // AutoCAD 2021 (g12)
        [InlineData("(variant-type)", "; error: too few arguments")] // AutoCAD 2021 (g12)
        [InlineData("(variant-type (variant 5) 1)", "; error: too many arguments")] // AutoCAD 2021 (g12)
        public void TheOldNames_AreTheFunctionsOfTheVlaxForms(string expression, string output) =>
            Assert.Equal(output, Out(expression));

        [Fact]
        public void TheOldNamesGiveWhatTheVlaxFormsGive()
        {
            LispInterpreter lisp = Loaded();
            lisp.Run("(setq a (safearray vlax-vbDouble '(0 . 2) '(0 . 1)))");
            lisp.Run("(setq b (vlax-make-safearray vlax-vbDouble '(0 . 2) '(0 . 1)))");

            Assert.Equal(
                lisp.Run(
                    "(list (vlax-safearray-get-dim b) (vlax-safearray-get-l-bound b 2) (vlax-safearray-get-u-bound b 1) (vlax-safearray-type b))"
                ).Output,
                lisp.Run(
                    "(list (safearray-get-dim a) (safearray-get-l-bound a 2) (safearray-get-u-bound a 1) (safearray-type a))"
                ).Output
            );
            Assert.Equal(
                lisp.Run("(vlax-variant-type (vlax-make-variant \"abc\"))").Output,
                lisp.Run("(variant-type (variant \"abc\"))").Output
            );
            Assert.True(new[] { "a", "b" }.All(n => lisp.GetValue(n) is LispSafeArray));
        }

        #endregion
    }
}
