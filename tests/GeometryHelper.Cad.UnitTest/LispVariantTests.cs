using GeometryHelper.Cad.Lisp;
using Xunit;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The variants and safearrays of ActiveX in a bare interpreter, against AutoCAD 2021's accoreconsole (probes a2, a3 and
    /// a4 of the L4 measurements): <c>vlax-make-variant</c>, <c>vlax-variant-*</c>, <c>vlax-make-safearray</c>,
    /// <c>vlax-safearray-*</c>, <c>vlax-3d-point</c>, <c>vlax-tmatrix</c>, the old <c>variant-value</c> and
    /// <c>safearray-value</c>, the <c>vlax-vb*</c> constants, and what exists before and after <c>(vl-load-com)</c>. Each row
    /// is an expression and the text AutoCAD printed for it, an error as the message after <c>; error: </c>; a row marked
    /// <c>ours</c> is a choice of the spec or of this part where AutoCAD was not measured.
    /// </summary>
    [Collection("LispSerial")]
    public class LispVariantTests
    {
        private static LispInterpreter Loaded()
        {
            var lisp = new LispInterpreter();
            lisp.Run("(vl-load-com)");
            return lisp;
        }

        private static string Out(string expression) => Loaded().Run(expression).Output;

        #region What exists before and after vl-load-com

        [Theory]
        [InlineData("(type vlax-make-variant)", "nil")] // AutoCAD 2021: nil (a3)
        [InlineData("(type vlax-3d-point)", "nil")] // AutoCAD 2021: nil (a3)
        [InlineData("(type vlax-get-acad-object)", "nil")] // AutoCAD 2021: nil (a3)
        [InlineData("(type vlax-true)", "nil")] // AutoCAD 2021: nil (a3)
        [InlineData("(type vlax-vbDouble)", "nil")] // AutoCAD 2021: nil (a4)
        [InlineData("(type variant-value)", "nil")] // ours: D201, defined by vl-load-com with the others
        [InlineData("(type safearray-value)", "nil")] // ours: D201
        [InlineData("(vlax-make-variant 5)", "; error: no function definition: VLAX-MAKE-VARIANT")] // ours: D201, L1 D16
        [InlineData(":vlax-true", ":vlax-true")] // AutoCAD 2021: :vlax-true, before vl-load-com too (a3)
        [InlineData(":vlax-false", ":vlax-false")] // AutoCAD 2021: :vlax-false (a2)
        [InlineData("(type :vlax-true)", "SYM")] // AutoCAD 2021: SYM (a2)
        [InlineData("(eq :vlax-true :vlax-true)", "T")] // AutoCAD 2021: T (a2)
        [InlineData(":foo", "nil")] // AutoCAD 2021: nil, an ordinary unbound symbol (a4)
        public void BeforeVlLoadCom_OnlyTheTwoSymbolsExist(string expression, string output)
        {
            Assert.Equal(output, new LispInterpreter().Run(expression).Output);
        }

        [Fact]
        public void AfterVlLoadCom_TheFunctionsAndConstantsAreDefined()
        {
            LispInterpreter lisp = Loaded();

            foreach (
                string name in new[]
                {
                    "vlax-make-variant",
                    "vlax-variant-type",
                    "vlax-variant-value",
                    "vlax-variant-change-type",
                    "vlax-make-safearray",
                    "vlax-safearray-fill",
                    "vlax-safearray->list",
                    "vlax-safearray-get-element",
                    "vlax-safearray-put-element",
                    "vlax-safearray-get-dim",
                    "vlax-safearray-get-l-bound",
                    "vlax-safearray-get-u-bound",
                    "vlax-safearray-type",
                    "vlax-3d-point",
                    "vlax-tmatrix",
                    "variant-value",
                    "safearray-value",
                }
            )
            {
                Assert.Equal("SUBR", lisp.Run("(type " + name + ")").Output); // AutoCAD 2021: SUBR (a2, a3)
            }
        }

        [Fact]
        public void VlLoadCom_GivesNothingAndASecondCallChangesNothing()
        {
            var lisp = new LispInterpreter();
            Assert.Equal("", lisp.Run("(vl-load-com)").Output); // AutoCAD 2021: nothing (a2)
            lisp.Run("(setq vlax-vbDouble 99)");
            Assert.Equal("", lisp.Run("(vl-load-com)").Output);
            Assert.Equal("99", lisp.Run("vlax-vbDouble").Output); // AutoCAD 2021: 99, an ordinary symbol (a4)
            Assert.Equal("5", lisp.Run("(setq vlax-vbDouble 5)").Output); // AutoCAD 2021: 5 (a4)
        }

        [Fact]
        public void EachInterpreterLoadsItsOwn()
        {
            LispInterpreter loaded = Loaded();
            var bare = new LispInterpreter();
            Assert.Equal("nil", bare.Run("(type vlax-make-variant)").Output); // ours: D202
            Assert.Equal("SUBR", loaded.Run("(type vlax-make-variant)").Output);
            Assert.Equal("SUBR", Loaded().Run("(type vlax-make-variant)").Output);
        }

        [Fact]
        public void TheOldNamesAreTheSameFunctions()
        {
            LispInterpreter lisp = Loaded();
            Assert.Equal("T", lisp.Run("(eq variant-value vlax-variant-value)").Output); // AutoCAD 2021: the same functions (a2)
            Assert.Equal("T", lisp.Run("(eq safearray-value vlax-safearray->list)").Output); // AutoCAD 2021: the same (a2)
            Assert.Equal("4", lisp.Run("(variant-value (vlax-make-variant 4))").Output); // AutoCAD 2021: 4 (a2)
            Assert.Equal(
                "(1.0 2.0 3.0)",
                lisp.Run("(safearray-value (vlax-variant-value (vlax-3d-point 1 2 3)))").Output
            ); // AutoCAD 2021: (1.0 2.0 3.0) (a2)
        }

        #endregion

        #region The constants

        [Theory]
        [InlineData("vlax-vbEmpty", "0")] // AutoCAD 2021: 0 (a2)
        [InlineData("vlax-vbNull", "1")] // AutoCAD 2021: 1 (a2)
        [InlineData("vlax-vbInteger", "2")] // AutoCAD 2021: 2 (a2)
        [InlineData("vlax-vbLong", "3")] // AutoCAD 2021: 3 (a2)
        [InlineData("vlax-vbSingle", "4")] // AutoCAD 2021: 4 (a2)
        [InlineData("vlax-vbDouble", "5")] // AutoCAD 2021: 5 (a2)
        [InlineData("vlax-vbString", "8")] // AutoCAD 2021: 8 (a2)
        [InlineData("vlax-vbObject", "9")] // AutoCAD 2021: 9 (a2)
        [InlineData("vlax-vbBoolean", "11")] // AutoCAD 2021: 11 (a2)
        [InlineData("vlax-vbVariant", "12")] // AutoCAD 2021: 12 (a2)
        [InlineData("vlax-vbArray", "8192")] // AutoCAD 2021: 8192 (a2)
        public void TheVbConstantsAreTheVartypes(string name, string output)
        {
            Assert.Equal(output, Out(name));
        }

        #endregion

        #region Variants

        [Theory]
        [InlineData("(vlax-3d-point 1 2 3)", "#<variant 8197 ...>")] // AutoCAD 2021: #<variant 8197 ...> (a2)
        [InlineData("(type (vlax-3d-point 1 2 3))", "variant")] // AutoCAD 2021: variant, in lower case (a2)
        [InlineData("(vlax-variant-type (vlax-3d-point 1 2 3))", "8197")] // AutoCAD 2021: 8197 (a2)
        [InlineData("(type (vlax-variant-value (vlax-3d-point 1 2 3)))", "safearray")] // AutoCAD 2021: safearray (a2)
        [InlineData("(vlax-make-variant 5)", "#<variant 3 5>")] // AutoCAD 2021: #<variant 3 5> (a2)
        [InlineData("(vlax-variant-type (vlax-make-variant 5))", "3")] // AutoCAD 2021: 3 (a2)
        [InlineData("(vlax-variant-value (vlax-make-variant 5))", "5")] // AutoCAD 2021: 5 (a2)
        [InlineData("(vlax-make-variant \"abc\")", "#<variant 8 abc>")] // AutoCAD 2021: #<variant 8 abc> (a2)
        [InlineData("(vlax-variant-type (vlax-make-variant \"abc\"))", "8")] // AutoCAD 2021: 8 (a2)
        [InlineData("(vlax-make-variant 1.5)", "#<variant 5 1.5>")] // AutoCAD 2021: #<variant 5 1.5> (a2)
        [InlineData("(vlax-variant-type (vlax-make-variant 1.5))", "5")] // AutoCAD 2021: 5 (a2)
        [InlineData("(vlax-make-variant nil)", "#<variant 0 >")] // AutoCAD 2021: #<variant 0 > (a2)
        [InlineData("(vlax-variant-type (vlax-make-variant nil))", "0")] // AutoCAD 2021: 0 (a2)
        [InlineData("(vlax-make-variant)", "#<variant 0 >")] // AutoCAD 2021: #<variant 0 > (a3)
        [InlineData("(vlax-variant-value (vlax-make-variant))", "nil")] // ours: D204, an empty variant reads as nil
        [InlineData("(vlax-variant-type (vlax-make-variant T))", "11")] // AutoCAD 2021: 11 (a2)
        [InlineData("(vlax-variant-value (vlax-make-variant T))", ":vlax-true")] // AutoCAD 2021: :vlax-true (a2)
        [InlineData("(vlax-make-variant T)", "#<variant 11 -1>")] // ours: D203, true prints -1 as 7 with type 11 does (a3)
        [InlineData("(vlax-make-variant :vlax-true)", "#<variant 11 -1>")] // AutoCAD 2021: #<variant 11 -1> (a3)
        [InlineData("(vlax-variant-type (vlax-make-variant :vlax-true))", "11")] // AutoCAD 2021: 11 (a3)
        [InlineData("(vlax-make-variant :vlax-false)", "#<variant 11 0>")] // ours: D203, false prints 0
        [InlineData("(vlax-variant-value (vlax-make-variant :vlax-false))", ":vlax-false")] // ours: D204
        [InlineData("(vlax-make-variant 7 3)", "#<variant 3 7>")] // AutoCAD 2021: #<variant 3 7> (a2)
        [InlineData("(vlax-variant-type (vlax-make-variant 7 5))", "5")] // AutoCAD 2021: 5 (a2)
        [InlineData("(vlax-variant-value (vlax-make-variant 7 5))", "7.0")] // AutoCAD 2021: 7.0 (a2)
        [InlineData("(vlax-variant-type (vlax-make-variant \"x\" 8))", "8")] // AutoCAD 2021: 8 (a2)
        [InlineData("(vlax-make-variant 70000)", "#<variant 3 70000>")] // AutoCAD 2021: #<variant 3 70000> (a3)
        [InlineData("(vlax-make-variant 7 2)", "#<variant 2 7>")] // AutoCAD 2021: #<variant 2 7> (a3)
        [InlineData("(vlax-variant-type (vlax-make-variant 7 2))", "2")] // AutoCAD 2021: 2 (a3)
        [InlineData("(vlax-make-variant 7 11)", "#<variant 11 -1>")] // AutoCAD 2021: #<variant 11 -1> (a3)
        [InlineData("(vlax-variant-value (vlax-make-variant 0 11))", ":vlax-false")] // AutoCAD 2021: :vlax-false (a3)
        [InlineData("(vlax-make-variant \"1.5\" 5)", "#<variant 5 1.5>")] // AutoCAD 2021: #<variant 5 1.5> (a3)
        [InlineData("(vlax-make-variant 1.5 3)", "#<variant 3 2>")] // AutoCAD 2021: #<variant 3 2> (a3)
        [InlineData("(vlax-make-variant 1.5 8)", "#<variant 8 1.5>")] // AutoCAD 2021: #<variant 8 1.5> (a3)
        [InlineData("(vlax-make-variant (vlax-3d-point 1 2 3))", "#<variant 8197 ...>")] // AutoCAD 2021: #<variant 8197 ...> (a2)
        [InlineData("(vlax-make-variant (vlax-make-variant 3))", "#<variant 3 3>")] // AutoCAD 2021: #<variant 3 3> (a3)
        [InlineData("(vlax-variant-type (vlax-make-variant (vlax-make-variant 3)))", "3")] // AutoCAD 2021: 3 (a3)
        [InlineData("(vlax-make-variant 1.0e20)", "#<variant 5 1E+20>")] // AutoCAD 2021: #<variant 5 1E+20> (a3)
        [InlineData("(vlax-make-variant 0.333333333)", "#<variant 5 0.333333333>")] // AutoCAD 2021: #<variant 5 0.333333333> (a3)
        [InlineData("(vlax-make-variant -2147483648)", "#<variant 5 -2147483648>")] // AutoCAD 2021: #<variant 5 -2147483648>, the reader makes a real (a3)
        [InlineData("(vlax-make-variant 1.0e-7)", "#<variant 5 1E-07>")] // ours: D204, E-07 in the exponent form
        [InlineData("(vlax-make-variant 0.00001)", "#<variant 5 1E-05>")] // ours: D204, the exponent form below 1E-4 as Visual Basic writes a Double
        [InlineData("(vlax-make-variant 0.0001)", "#<variant 5 0.0001>")] // ours: D204, not yet in exponent form
        [InlineData("(vlax-make-variant 1.0e15)", "#<variant 5 1E+15>")] // ours: D204, past 1E15
        [InlineData("(vlax-make-variant 123456789012345.0)", "#<variant 5 123456789012345>")] // ours: D204, 15 digits
        [InlineData("(vlax-make-variant 0.1234567890123456789)", "#<variant 5 0.123456789012346>")] // ours: D204, at most 15 digits
        [InlineData("(vlax-make-variant -0.5)", "#<variant 5 -0.5>")] // ours: D204
        [InlineData("(vlax-make-variant 0.0)", "#<variant 5 0>")] // ours: D204
        [InlineData("(vlax-make-variant 2.5 4)", "#<variant 4 2.5>")] // ours: D204, a single prints with 7 digits
        [InlineData("(vlax-make-variant 0 0)", "#<variant 0 >")] // ours: D204, the empty type keeps no value
        [InlineData("(vlax-make-variant 0 1)", "#<variant 1 >")] // ours: D204, the null type keeps no value
        public void MakingAVariant_GivesWhatAutoCadGives(string expression, string output)
        {
            Assert.Equal(output, Out(expression));
        }

        [Theory]
        [InlineData("(vlax-make-variant 'abc)", "; error: lisp value has no coercion to VARIANT with this type:  ABC")] // AutoCAD 2021 (a3)
        [InlineData(
            "(vlax-make-variant '(1 2))",
            "; error: lisp value has no coercion to VARIANT with this type:  (1 2)"
        )] // AutoCAD 2021 (a2)
        [InlineData("(vlax-make-variant 1 2 3)", "; error: too many arguments")] // AutoCAD 2021: too many arguments (a3)
        [InlineData(
            "(vlax-make-variant \"abc\" 3)",
            "; error: lisp value has no coercion to VARIANT with this type:  \"abc\""
        )] // ours: D204, the error of the fill
        [InlineData(
            "(vlax-make-variant 70000 2)",
            "; error: lisp value has no coercion to VARIANT with this type:  70000"
        )] // ours: D204, past a 16-bit integer
        [InlineData("(vlax-make-variant 1 99)", "; error: lisp value has no coercion to VARIANT with this type:  1")] // ours: D204, a type with no conversion
        [InlineData("(vlax-make-variant 1 1.5)", "; error: bad argument type: fixnump: 1.5")] // ours: the type is an integer
        [InlineData("(vlax-variant-type 5)", "; error: bad argument type: variantp 5")] // AutoCAD 2021 (a3)
        [InlineData("(vlax-variant-value 5)", "; error: bad argument type: variantp 5")] // AutoCAD 2021 (a2)
        [InlineData("(variant-value 5)", "; error: bad argument type: variantp 5")] // AutoCAD 2021: the same function (a2)
        [InlineData("(vlax-variant-change-type 5 8)", "; error: bad argument type: variantp 5")] // AutoCAD 2021 (a3)
        [InlineData("(vlax-variant-type)", "; error: too few arguments")] // ours: L1 D16
        [InlineData(
            "(vlax-variant-value (vlax-variant-change-type (vlax-3d-point 1 2 3) vlax-vbString))",
            "; error: bad argument type: variantp nil"
        )] // AutoCAD 2021: an array does not change to a scalar, nil, then variantp nil (a3)
        public void AVariantThatCannotBeMade_GivesAnError(string expression, string output)
        {
            Assert.Equal(output, Out(expression));
        }

        [Theory]
        [InlineData("(vlax-variant-change-type (vlax-make-variant 7) 8)", "#<variant 8 7>")] // AutoCAD 2021: #<variant 8 7> (a2)
        [InlineData("(vlax-variant-value (vlax-variant-change-type (vlax-make-variant 7) 8))", "\"7\"")] // AutoCAD 2021: "7" (a2)
        [InlineData("(vlax-variant-value (vlax-variant-change-type (vlax-make-variant \"12\") vlax-vbInteger))", "12")] // AutoCAD 2021: 12 (a2)
        [InlineData("(vlax-variant-change-type (vlax-make-variant \"abc\") vlax-vbInteger)", "nil")] // AutoCAD 2021: nil (a2)
        [InlineData("(vlax-variant-change-type (vlax-make-variant 1.7) vlax-vbInteger)", "#<variant 2 2>")] // AutoCAD 2021: #<variant 2 2> (a3)
        [InlineData("(vlax-variant-value (vlax-variant-change-type (vlax-make-variant 2.5) vlax-vbInteger))", "2")] // AutoCAD 2021: 2, half to even (a3)
        [InlineData("(vlax-variant-value (vlax-variant-change-type (vlax-make-variant 3.5) vlax-vbLong))", "4")] // AutoCAD 2021: 4 (a3)
        [InlineData("(vlax-variant-value (vlax-variant-change-type (vlax-make-variant 1.5) vlax-vbLong))", "2")] // AutoCAD 2021: 2 (a3, 1.5 to 3)
        [InlineData("(vlax-variant-value (vlax-variant-change-type (vlax-make-variant -2.5) vlax-vbLong))", "-2")] // ours: D204, half to even below nought too
        [InlineData(
            "(vlax-variant-value (vlax-variant-change-type (vlax-make-variant 0) vlax-vbBoolean))",
            ":vlax-false"
        )] // AutoCAD 2021: :vlax-false (a3)
        [InlineData(
            "(vlax-variant-value (vlax-variant-change-type (vlax-make-variant 5) vlax-vbBoolean))",
            ":vlax-true"
        )] // AutoCAD 2021: :vlax-true (a3)
        [InlineData(
            "(vlax-variant-value (vlax-variant-change-type (vlax-make-variant \"1.25\") vlax-vbDouble))",
            "1.25"
        )] // AutoCAD 2021: 1.25 (a3)
        [InlineData(
            "(vlax-variant-value (vlax-variant-change-type (vlax-make-variant 1.25) vlax-vbString))",
            "\"1.25\""
        )] // AutoCAD 2021: "1.25" (a3)
        [InlineData(
            "(vlax-variant-value (vlax-variant-change-type (vlax-make-variant 1e20) vlax-vbString))",
            "\"1E+20\""
        )] // AutoCAD 2021: "1E+20" (a3)
        [InlineData(
            "(vlax-variant-value (vlax-variant-change-type (vlax-make-variant :vlax-true) vlax-vbString))",
            "\"-1\""
        )] // AutoCAD 2021: "-1" (a3)
        [InlineData(
            "(vlax-variant-value (vlax-variant-change-type (vlax-make-variant :vlax-false) vlax-vbString))",
            "\"0\""
        )] // ours: D204
        [InlineData(
            "(vlax-variant-value (vlax-variant-change-type (vlax-make-variant :vlax-true) vlax-vbInteger))",
            "-1"
        )] // ours: D204, true is -1
        [InlineData(
            "(vlax-variant-value (vlax-variant-change-type (vlax-make-variant 0.333333333) vlax-vbString))",
            "\"0.333333333\""
        )] // AutoCAD 2021: nine digits kept (a3)
        [InlineData("(vlax-variant-value (vlax-variant-change-type (vlax-make-variant \" 7 \") vlax-vbDouble))", "7.0")] // ours: D204, blanks around a number
        [InlineData("(vlax-variant-value (vlax-variant-change-type (vlax-make-variant \"1.6\") vlax-vbInteger))", "2")] // ours: D204, a string parsed then rounded
        [InlineData(
            "(vlax-variant-value (vlax-variant-change-type (vlax-make-variant \"true\") vlax-vbBoolean))",
            ":vlax-true"
        )] // ours: D204
        [InlineData(
            "(vlax-variant-value (vlax-variant-change-type (vlax-make-variant \"False\") vlax-vbBoolean))",
            ":vlax-false"
        )] // ours: D204
        [InlineData("(vlax-variant-change-type (vlax-make-variant \"yes\") vlax-vbBoolean)", "nil")] // ours: D204
        [InlineData("(vlax-variant-change-type (vlax-make-variant \"NaN\") vlax-vbDouble)", "nil")] // ours: D204, no infinities
        [InlineData("(vlax-variant-change-type (vlax-make-variant 1e10) vlax-vbLong)", "nil")] // ours: D204, past 32 bits
        [InlineData("(vlax-variant-change-type (vlax-make-variant 40000) vlax-vbInteger)", "nil")] // ours: D204, past 16 bits
        [InlineData("(vlax-variant-value (vlax-variant-change-type (vlax-make-variant 3) vlax-vbSingle))", "3.0")] // ours: D204
        [InlineData("(vlax-variant-change-type (vlax-make-variant 3) 99)", "nil")] // ours: D204, no such type
        [InlineData("(vlax-variant-change-type (vlax-make-variant 3) vlax-vbObject)", "nil")] // ours: D204
        [InlineData("(vlax-variant-change-type (vlax-3d-point 1 2 3) vlax-vbDouble)", "nil")] // AutoCAD 2021: nil, an array to a scalar (a3)
        [InlineData("(vlax-variant-type (vlax-variant-change-type (vlax-3d-point 1 2 3) 8197))", "8197")] // ours: D204, an array keeps its type
        [InlineData("(vlax-variant-value (vlax-variant-change-type (vlax-make-variant nil) vlax-vbString))", "\"\"")] // ours: D204, empty is the empty string
        [InlineData("(vlax-variant-value (vlax-variant-change-type (vlax-make-variant nil) vlax-vbDouble))", "0.0")] // ours: D204, empty is nought
        public void ChangingTheTypeOfAVariant_GivesWhatAutoCadGives(string expression, string output)
        {
            Assert.Equal(output, Out(expression));
        }

        [Fact]
        public void ChangingTheTypeLeavesTheVariantAlone()
        {
            LispInterpreter lisp = Loaded();
            lisp.Run("(setq v (vlax-make-variant 7))");
            lisp.Run("(vlax-variant-change-type v vlax-vbString)");
            Assert.Equal("#<variant 3 7>", lisp.Run("v").Output); // ours: a variant is a value
            Assert.Equal("T", lisp.Run("(eq v (vlax-variant-change-type v vlax-vbLong))").Output); // ours: the same type gives the same value
        }

        [Theory]
        [InlineData("(vl-symbol-name (type (vlax-make-variant 1)))", "\"variant\"")] // AutoCAD 2021: "variant" (a4)
        [InlineData("(vl-symbol-name (type (vlax-make-safearray 5 '(0 . 1))))", "\"safearray\"")] // AutoCAD 2021: "safearray" (a4)
        [InlineData("(eq (type (vlax-make-variant 1)) 'variant)", "T")] // AutoCAD 2021: T (a4)
        [InlineData("(eq (type (vlax-make-variant 1)) 'VARIANT)", "T")] // AutoCAD 2021: T (a4)
        [InlineData("(eq (type (vlax-make-safearray 5 '(0 . 1))) 'safearray)", "T")] // AutoCAD 2021: T (a4)
        [InlineData("(type (vlax-make-safearray 5 '(0 . 1)))", "safearray")] // AutoCAD 2021: safearray (a2)
        [InlineData("(type 'variant)", "SYM")] // ours: the type name is still an ordinary symbol
        [InlineData("(vl-princ-to-string 'variant)", "\"variant\"")] // ours: D203, the symbol prints as the type does
        public void TheTypesPrintInLowerCase(string expression, string output)
        {
            Assert.Equal(output, Out(expression));
        }

        [Theory]
        [InlineData("(vl-princ-to-string (vlax-make-variant \"a\"))", "\"#<variant 8 a>\"")] // AutoCAD 2021: "#<variant 8 a>" (a3)
        [InlineData("(vl-prin1-to-string (vlax-make-variant \"a b\"))", "\"#<variant 8 a b>\"")] // AutoCAD 2021: the string raw (a3)
        [InlineData("(vl-prin1-to-string (vlax-make-variant \"a\\\"b\"))", "\"#<variant 8 a\\\"b>\"")] // AutoCAD 2021: #<variant 8 a"b>, the quote raw (a3)
        [InlineData(
            "(list (vlax-make-variant 2) (vlax-make-safearray vlax-vbDouble '(0 . 1)))",
            "(#<variant 3 2> #<safearray...>)"
        )] // AutoCAD 2021: (#<variant 3 2> #<safearray...>) (a3)
        [InlineData("(vlax-make-safearray vlax-vbDouble '(0 . 2))", "#<safearray...>")] // AutoCAD 2021: #<safearray...> (a2)
        public void TheVariantAndTheSafearrayPrint(string expression, string output)
        {
            Assert.Equal(output, Out(expression));
        }

        [Theory]
        [InlineData("(equal (vlax-3d-point 1 2 3) (vlax-3d-point 1 2 3))", "T")] // AutoCAD 2021: T (a3)
        [InlineData("(equal (vlax-make-variant 1) (vlax-make-variant 1))", "T")] // AutoCAD 2021: T (a3)
        [InlineData("(equal (vlax-make-variant 1) (vlax-make-variant 2))", "nil")] // ours: D203, the value
        [InlineData("(equal (vlax-make-variant 1) (vlax-make-variant 1.0))", "nil")] // ours: D203, the type
        [InlineData("(equal (vlax-make-variant 1) (vlax-make-variant 1 5))", "nil")] // ours: D203, the type
        [InlineData("(equal (vlax-make-variant \"a\") (vlax-make-variant \"a\"))", "T")] // ours: D203
        [InlineData("(equal (vlax-make-variant \"a\") (vlax-make-variant \"A\"))", "nil")] // ours: D203, strings by case
        [InlineData("(equal (vlax-make-variant T) (vlax-make-variant :vlax-true))", "T")] // ours: D203
        [InlineData("(equal (vlax-make-variant T) (vlax-make-variant :vlax-false))", "nil")] // ours: D203
        [InlineData("(equal (vlax-make-variant nil) (vlax-make-variant))", "T")] // ours: D203, two empty variants
        [InlineData("(equal (vlax-3d-point 1 2 3) (vlax-3d-point 1 2 4))", "nil")] // ours: D203
        [InlineData("(equal (vlax-3d-point 1 2 3) (vlax-3d-point 1 2 3.04) 0.05)", "T")] // ours: D203, within the fuzz
        [InlineData("(eq (vlax-3d-point 1 2 3) (vlax-3d-point 1 2 3))", "nil")] // ours: D203, eq is by reference
        [InlineData("(equal (vlax-make-safearray 5 '(0 . 2)) (vlax-make-safearray 5 '(0 . 2)))", "T")] // ours: D203, two arrays of the same elements
        [InlineData("(equal (vlax-make-safearray 5 '(0 . 2)) (vlax-make-safearray 5 '(1 . 3)))", "nil")] // ours: D203, the bounds
        [InlineData("(equal (vlax-make-safearray 5 '(0 . 2)) (vlax-make-safearray 3 '(0 . 2)))", "nil")] // ours: D203, the element type
        [InlineData("(equal (vlax-make-safearray 5 '(0 . 3)) (vlax-make-safearray 5 '(0 . 1) '(0 . 1)))", "nil")] // ours: D203, the dimensions
        [InlineData("(equal (vlax-make-safearray 11 '(0 . 1)) (vlax-make-safearray 11 '(0 . 1)))", "T")] // ours: D203, booleans
        [InlineData("(equal (vlax-make-safearray 12 '(0 . 1)) (vlax-make-safearray 12 '(0 . 1)))", "T")] // ours: D203, variants inside
        [InlineData("(equal (vlax-make-safearray 9 '(0 . 1)) (vlax-make-safearray 9 '(0 . 1)))", "T")] // ours: D203, objects not made yet
        [InlineData("(equal (list (vlax-make-variant 1)) (list (vlax-make-variant 1)))", "T")] // ours: D203, inside a list
        public void EqualComparesTypeAndValue(string expression, string output)
        {
            Assert.Equal(output, Out(expression));
        }

        #endregion

        #region Safearrays

        [Theory]
        [InlineData("(vlax-safearray->list (vlax-variant-value (vlax-3d-point 1 2 3)))", "(1.0 2.0 3.0)")] // AutoCAD 2021: (1.0 2.0 3.0) (a2)
        [InlineData("(vlax-safearray->list (vlax-variant-value (vlax-3d-point '(4 5))))", "(4.0 5.0 0.0)")] // AutoCAD 2021: (4.0 5.0 0.0) (a2)
        [InlineData("(vlax-3d-point '(4 5))", "#<variant 8197 ...>")] // AutoCAD 2021: #<variant 8197 ...> (a2)
        [InlineData("(vlax-3d-point 1 2)", "#<variant 8197 ...>")] // AutoCAD 2021 (a3)
        [InlineData("(vlax-safearray->list (vlax-variant-value (vlax-3d-point 1 2)))", "(1.0 2.0 0.0)")] // AutoCAD 2021: (1.0 2.0 0.0) (a3)
        [InlineData("(vlax-safearray->list (vlax-variant-value (vlax-3d-point '(1 2 3))))", "(1.0 2.0 3.0)")] // AutoCAD 2021: a list of three (a2, a3)
        [InlineData("(vlax-3d-point 1)", "; error: bad 3D point: 1")] // AutoCAD 2021 (a3)
        [InlineData("(vlax-3d-point '(1 2 3 4))", "; error: bad 3D point list: (1 2 3 4)")] // AutoCAD 2021 (a3)
        [InlineData("(vlax-3d-point \"a\" 1)", "; error: bad argument type: numberp: \"a\"")] // AutoCAD 2021 (a3)
        [InlineData("(vlax-3d-point '(1 \"a\"))", "; error: bad argument type: numberp: \"a\"")] // AutoCAD 2021 (a3)
        [InlineData("(vlax-3d-point '(1))", "; error: bad 3D point list: (1)")] // ours: D206
        [InlineData("(vlax-3d-point nil)", "; error: bad 3D point list: nil")] // ours: D206, nil is the empty list
        [InlineData("(vlax-3d-point \"a\")", "; error: bad 3D point: \"a\"")] // ours: D206
        [InlineData("(vlax-3d-point '(1 . 2))", "; error: bad 3D point list: (1 . 2)")] // ours: D206, a dotted pair is no list of numbers
        [InlineData("(vlax-3d-point 1 2 3 4)", "; error: too many arguments")] // ours: L1 D16
        [InlineData("(vlax-3d-point)", "; error: too few arguments")] // ours: L1 D16
        public void The3dPoint_IsAVariantOfThreeDoubles(string expression, string output)
        {
            Assert.Equal(output, Out(expression));
        }

        [Theory]
        [InlineData("(vlax-tmatrix '((1 0 0 0) (0 1 0 0) (0 0 1 0) (0 0 0 1)))", "#<variant 8197 ...>")] // AutoCAD 2021: #<variant 8197 ...> (a2)
        [InlineData(
            "(vlax-safearray->list (vlax-variant-value (vlax-tmatrix '((1 0 0 5) (0 1 0 0) (0 0 1 0) (0 0 0 1)))))",
            "((1.0 0.0 0.0 5.0) (0.0 1.0 0.0 0.0) (0.0 0.0 1.0 0.0) (0.0 0.0 0.0 1.0))"
        )] // AutoCAD 2021 (a3)
        [InlineData(
            "(vlax-safearray-get-dim (vlax-variant-value (vlax-tmatrix '((1 0 0 0) (0 1 0 0) (0 0 1 0) (0 0 0 1)))))",
            "2"
        )] // AutoCAD 2021: two dimensions (a3)
        [InlineData(
            "(vlax-safearray-get-u-bound (vlax-variant-value (vlax-tmatrix '((1 0 0 0) (0 1 0 0) (0 0 1 0) (0 0 0 1)))) 2)",
            "3"
        )] // AutoCAD 2021: 0..3 (a3)
        [InlineData("(vlax-tmatrix '((1 0 0) (0 1 0)))", "; error: bad transform matrix list: ((1 0 0) (0 1 0))")] // AutoCAD 2021 (a3)
        [InlineData(
            "(vlax-tmatrix '((1 0 0 0) (0 1 0 0) (0 0 1 0)))",
            "; error: bad transform matrix list: ((1 0 0 0) (0 1 0 0) (0 0 1 0))"
        )] // ours: D207, three rows
        [InlineData(
            "(vlax-tmatrix '((1 0 0 0) (0 1 0 0) (0 0 1 0) (0 0 0 1) (0 0 0 1)))",
            "; error: bad transform matrix list: ((1 0 0 0) (0 1 0 0) (0 0 1 0) (0 0 0 1) (0 0 0 1))"
        )] // ours: D207, five rows
        [InlineData(
            "(vlax-tmatrix '((1 0 0 0) (0 1 0 0) (0 0 1 0) (0 0 0 \"a\")))",
            "; error: bad transform matrix list: ((1 0 0 0) (0 1 0 0) (0 0 1 0) (0 0 0 \"a\"))"
        )] // ours: D207, a non-number
        [InlineData("(vlax-tmatrix 5)", "; error: bad transform matrix list: 5")] // ours: D207
        [InlineData("(vlax-tmatrix nil)", "; error: bad transform matrix list: nil")] // ours: D207
        public void TheTransformMatrix_IsAVariantOfA4By4Array(string expression, string output)
        {
            Assert.Equal(output, Out(expression));
        }

        [Theory]
        [InlineData("(vlax-safearray-fill (vlax-make-safearray vlax-vbDouble '(0 . 2)) '(1 2 3))", "#<safearray...>")] // AutoCAD 2021: #<safearray...> (a2)
        [InlineData(
            "(vlax-safearray->list (vlax-safearray-fill (vlax-make-safearray vlax-vbDouble '(0 . 2)) '(1 2 3)))",
            "(1.0 2.0 3.0)"
        )] // AutoCAD 2021: (1.0 2.0 3.0) (a2)
        [InlineData("(vlax-safearray->list (vlax-make-safearray vlax-vbString '(1 . 2)))", "(\"\" \"\")")] // AutoCAD 2021: ("" "") (a2)
        [InlineData(
            "(vlax-safearray->list (vlax-safearray-fill (vlax-make-safearray vlax-vbVariant '(0 . 1)) '(1 \"a\")))",
            "(#<variant 3 1> #<variant 8 a>)"
        )] // AutoCAD 2021 (a2)
        [InlineData(
            "(vlax-safearray->list (vlax-safearray-fill (vlax-make-safearray vlax-vbDouble '(0 . 1) '(1 . 2)) '((1 2) (3 4))))",
            "((1.0 2.0) (3.0 4.0))"
        )] // AutoCAD 2021 (a2)
        [InlineData("(vlax-safearray->list (vlax-make-safearray vlax-vbInteger '(0 . 1)))", "(0 0)")] // AutoCAD 2021: (0 0) (a3)
        [InlineData("(vlax-safearray->list (vlax-make-safearray vlax-vbLong '(0 . 1)))", "(0 0)")] // ours: D205, the integer kinds
        [InlineData("(vlax-safearray->list (vlax-make-safearray vlax-vbSingle '(0 . 1)))", "(0.0 0.0)")] // ours: D205, singles start as 0.0
        [InlineData("(vlax-safearray->list (vlax-make-safearray vlax-vbDouble '(0 . 1)))", "(0.0 0.0)")] // AutoCAD 2021: doubles start as 0.0 (a3)
        [InlineData(
            "(vlax-safearray->list (vlax-make-safearray vlax-vbBoolean '(0 . 1)))",
            "(:vlax-false :vlax-false)"
        )] // AutoCAD 2021 (a3)
        [InlineData(
            "(vlax-safearray->list (vlax-make-safearray vlax-vbVariant '(0 . 1)))",
            "(#<variant 0 > #<variant 0 >)"
        )] // AutoCAD 2021 (a3)
        [InlineData("(vlax-safearray->list (vlax-make-safearray vlax-vbObject '(0 . 1)))", "(nil nil)")] // AutoCAD 2021: (nil nil) (a3)
        [InlineData("(vlax-safearray->list (vlax-make-safearray 99 '(0 . 1)))", "(nil nil)")] // ours: D205, any other type starts as nil
        [InlineData("(vlax-make-safearray 99 '(0 . 1))", "#<safearray...>")] // AutoCAD 2021: #<safearray...>, any type number (a3)
        [InlineData(
            "(vlax-safearray->list (vlax-safearray-fill (vlax-make-safearray vlax-vbString '(0 . 1)) '(\"a\" 1)))",
            "(\"a\" \"1\")"
        )] // AutoCAD 2021: ("a" "1") (a3)
        [InlineData(
            "(vlax-safearray->list (vlax-safearray-fill (vlax-make-safearray vlax-vbInteger '(0 . 1)) '(1.6 2)))",
            "(2 2)"
        )] // AutoCAD 2021: (2 2) (a3)
        [InlineData(
            "(vlax-safearray->list (vlax-safearray-fill (vlax-make-safearray vlax-vbInteger '(0 . 2)) '(0.5 1.5 2.5)))",
            "(0 2 2)"
        )] // ours: D205, half to even as the variants
        [InlineData(
            "(vlax-safearray->list (vlax-safearray-fill (vlax-make-safearray vlax-vbDouble '(0 . 1)) '(\"1.5\" 2)))",
            "(1.5 2.0)"
        )] // ours: D205, a numeric string parsed
        [InlineData(
            "(vlax-safearray->list (vlax-safearray-fill (vlax-make-safearray vlax-vbBoolean '(0 . 3)) '(T nil :vlax-false 5)))",
            "(:vlax-true :vlax-false :vlax-false :vlax-true)"
        )] // ours: D205
        [InlineData(
            "(vlax-safearray->list (vlax-safearray-fill (vlax-make-safearray vlax-vbVariant '(0 . 3)) '(nil 2.5 T :vlax-false)))",
            "(#<variant 0 > #<variant 5 2.5> #<variant 11 -1> #<variant 11 0>)"
        )] // ours: D205, each item as vlax-make-variant makes it
        [InlineData(
            "(vlax-safearray->list (vlax-safearray-fill (vlax-make-safearray vlax-vbDouble '(0 . 1)) (list (vlax-make-variant 3) (vlax-make-variant 4.5))))",
            "(3.0 4.5)"
        )] // ours: D205, a variant gives its value
        [InlineData(
            "(vlax-safearray->list (vlax-safearray-fill (vlax-make-safearray vlax-vbDouble '(0 . 1)) '(\"a\" 1)))",
            "; error: lisp value has no coercion to VARIANT with this type:  \"a\""
        )] // AutoCAD 2021 (a3)
        [InlineData(
            "(vlax-safearray-fill (vlax-make-safearray vlax-vbDouble '(0 . 2)) '(1 2))",
            "; error: vlax-safearray-fill failed.  Invalid initialization list. #<safearray...> (1 2)"
        )] // AutoCAD 2021 (a2)
        [InlineData(
            "(vlax-safearray-fill (vlax-make-safearray vlax-vbDouble '(0 . 2)) '(1 2 3 4))",
            "; error: vlax-safearray-fill failed.  Invalid initialization list. #<safearray...> (1 2 3 4)"
        )] // AutoCAD 2021 (a3)
        [InlineData(
            "(vlax-safearray-fill (vlax-make-safearray vlax-vbDouble '(0 . 1) '(0 . 1)) '(1 2 3 4))",
            "; error: vlax-safearray-fill failed.  Invalid initialization list. #<safearray...> (1 2 3 4)"
        )] // ours: D205, two dimensions want nested lists
        [InlineData(
            "(vlax-safearray-fill (vlax-make-safearray vlax-vbDouble '(0 . 1) '(0 . 1)) '((1 2) (3)))",
            "; error: vlax-safearray-fill failed.  Invalid initialization list. #<safearray...> ((1 2) (3))"
        )] // ours: D205, a short row
        [InlineData(
            "(vlax-safearray-fill (vlax-make-safearray vlax-vbDouble '(0 . 1)) '(1 . 2))",
            "; error: vlax-safearray-fill failed.  Invalid initialization list. #<safearray...> (1 . 2)"
        )] // ours: D205, a dotted list
        [InlineData(
            "(vlax-safearray-fill (vlax-make-safearray vlax-vbDouble '(0 . 1)) 5)",
            "; error: bad argument type: listp 5"
        )] // ours: D205
        [InlineData("(vlax-safearray-fill 5 '(1 2))", "; error: bad argument type: safearrayp 5")] // AutoCAD 2021: safearrayp (a2, a3)
        [InlineData(
            "(vlax-safearray-fill (vlax-make-safearray vlax-vbObject '(0 . 1)) '(1 nil))",
            "; error: lisp value has no coercion to VARIANT with this type:  1"
        )] // ours: D205, an integer is no object
        [InlineData(
            "(vlax-safearray->list (vlax-safearray-fill (vlax-make-safearray vlax-vbObject '(0 . 1)) '(nil nil)))",
            "(nil nil)"
        )] // ours: D205
        public void FillingASafearray_GivesWhatAutoCadGives(string expression, string output)
        {
            Assert.Equal(output, Out(expression));
        }

        [Fact]
        public void AFillThatFailsInTheMiddle_ChangesNothing()
        {
            LispInterpreter lisp = Loaded();
            lisp.Run("(setq a (vlax-safearray-fill (vlax-make-safearray vlax-vbDouble '(0 . 2)) '(1 2 3)))");
            lisp.Run("(vl-catch-all-apply 'vlax-safearray-fill (list a '(7 \"x\" 9)))");
            Assert.Equal("(1.0 2.0 3.0)", lisp.Run("(vlax-safearray->list a)").Output); // ours: D205, all or nothing
            lisp.Run("(vl-catch-all-apply 'vlax-safearray-fill (list a '(7 8)))");
            Assert.Equal("(1.0 2.0 3.0)", lisp.Run("(vlax-safearray->list a)").Output);
        }

        [Fact]
        public void TheFillGivesTheArrayItself()
        {
            LispInterpreter lisp = Loaded();
            lisp.Run("(setq a (vlax-make-safearray vlax-vbDouble '(0 . 2)))");
            Assert.Equal("T", lisp.Run("(eq a (vlax-safearray-fill a '(1 2 3)))").Output); // ours: D205, the array, not a copy
        }

        [Theory]
        [InlineData(
            "(vlax-make-safearray vlax-vbDouble '(0 . -1))",
            "; error: Upper bound in SAFEARRAYBOUND occurred to be less than lower."
        )] // AutoCAD 2021 (a3)
        [InlineData(
            "(vlax-make-safearray vlax-vbDouble '(2 . 1))",
            "; error: Upper bound in SAFEARRAYBOUND occurred to be less than lower."
        )] // AutoCAD 2021 (a3)
        [InlineData(
            "(vlax-safearray->list (vlax-make-safearray vlax-vbDouble '(0 . -1)))",
            "; error: Upper bound in SAFEARRAYBOUND occurred to be less than lower."
        )] // AutoCAD 2021 (a3)
        [InlineData("(vlax-make-safearray vlax-vbDouble)", "; error: too few arguments")] // AutoCAD 2021 (a3)
        [InlineData(
            "(vlax-make-safearray vlax-vbDouble 5)",
            "; error: Array specification (bounds) is not a dotted pair."
        )] // AutoCAD 2021 (a3)
        [InlineData(
            "(vlax-make-safearray vlax-vbDouble '(0 1))",
            "; error: Array specification (bounds) is not a dotted pair."
        )] // ours: D205, a list is no dotted pair
        [InlineData(
            "(vlax-make-safearray vlax-vbDouble '(0 . 1) 7)",
            "; error: Array specification (bounds) is not a dotted pair."
        )] // ours: D205, each dimension
        [InlineData(
            "(vlax-make-safearray vlax-vbDouble nil)",
            "; error: Array specification (bounds) is not a dotted pair."
        )] // ours: D205
        [InlineData("(vlax-make-safearray 5.0 '(0 . 1))", "; error: bad argument type: fixnump: 5.0")] // ours: the type is an integer
        [InlineData("(vlax-make-safearray vlax-vbDouble '(0 . 1.5))", "; error: bad argument type: fixnump: 1.5")] // ours: the bounds are integers
        [InlineData(
            "(vlax-make-safearray vlax-vbDouble '(0 . 100000000))",
            "; error: ActiveX Server returned an error: Out of memory"
        )] // ours: D205, a bound typed by mistake
        [InlineData(
            "(vlax-make-safearray vlax-vbDouble '(0 . 5000) '(0 . 5000))",
            "; error: ActiveX Server returned an error: Out of memory"
        )] // ours: D205, 25 million elements
        [InlineData(
            "(vlax-make-safearray vlax-vbDouble '(-2147483647 . 2147483647))",
            "; error: ActiveX Server returned an error: Out of memory"
        )] // ours: D205, no overflow of the count
        [InlineData("(vlax-safearray-get-l-bound (vlax-make-safearray vlax-vbDouble '(-3 . 4)) 1)", "-3")] // ours: D205, negative bounds
        [InlineData("(vlax-safearray-get-u-bound (vlax-make-safearray vlax-vbDouble '(-3 . 4)) 1)", "4")] // ours: D205
        public void MakingASafearray_GivesWhatAutoCadGives(string expression, string output)
        {
            Assert.Equal(output, Out(expression));
        }

        [Fact]
        public void ASafearrayOfOneDimension_IsReadAndWrittenByIndex()
        {
            LispInterpreter lisp = Loaded();
            lisp.Run("(setq a (vlax-make-safearray vlax-vbDouble '(0 . 2)))");
            lisp.Run("(vlax-safearray-fill a '(1 2 3))");
            Assert.Equal("2.0", lisp.Run("(vlax-safearray-get-element a 1)").Output); // AutoCAD 2021: 2.0 (a2)
            Assert.Equal("9.5", lisp.Run("(vlax-safearray-put-element a 1 9.5)").Output); // AutoCAD 2021: 9.5 (a2)
            Assert.Equal("(1.0 9.5 3.0)", lisp.Run("(vlax-safearray->list a)").Output); // AutoCAD 2021 (a2)
            Assert.Equal("1", lisp.Run("(vlax-safearray-get-dim a)").Output); // AutoCAD 2021: 1 (a2)
            Assert.Equal("0", lisp.Run("(vlax-safearray-get-l-bound a 1)").Output); // AutoCAD 2021: 0 (a2)
            Assert.Equal("2", lisp.Run("(vlax-safearray-get-u-bound a 1)").Output); // AutoCAD 2021: 2 (a2)
            Assert.Equal("5", lisp.Run("(vlax-safearray-type a)").Output); // AutoCAD 2021: 5 (a2)
            Assert.Equal(
                "; error: ActiveX Server returned an error: Invalid index",
                lisp.Run("(vlax-safearray-get-element a 5)").Output
            ); // AutoCAD 2021 (a2)
            Assert.Equal(
                "; error: ActiveX Server returned an error: Invalid index",
                lisp.Run("(vlax-safearray-get-element a -1)").Output
            ); // ours: D205, below the lower bound
            Assert.Equal(
                "; error: ActiveX Server returned an error: Invalid index",
                lisp.Run("(vlax-safearray-put-element a 3 1.0)").Output
            ); // ours: D205
            Assert.Equal("(1.0 9.5 3.0)", lisp.Run("(vlax-safearray->list a)").Output);
        }

        [Fact]
        public void ASafearrayWithABoundOtherThanNought_IsIndexedByIt()
        {
            LispInterpreter lisp = Loaded();
            lisp.Run("(setq a (vlax-make-safearray vlax-vbInteger '(5 . 7)))");
            lisp.Run("(vlax-safearray-fill a '(10 20 30))");
            Assert.Equal("10", lisp.Run("(vlax-safearray-get-element a 5)").Output); // ours: D205
            Assert.Equal("30", lisp.Run("(vlax-safearray-get-element a 7)").Output);
            Assert.Equal(
                "; error: ActiveX Server returned an error: Invalid index",
                lisp.Run("(vlax-safearray-get-element a 0)").Output
            );
        }

        [Fact]
        public void ASafearrayOfTwoDimensions_IsIndexedByBoth()
        {
            LispInterpreter lisp = Loaded();
            lisp.Run("(setq g (vlax-make-safearray vlax-vbDouble '(1 . 2) '(0 . 2)))");
            Assert.Equal("2", lisp.Run("(vlax-safearray-get-dim g)").Output); // AutoCAD 2021: 2 (a3)
            Assert.Equal("1", lisp.Run("(vlax-safearray-get-l-bound g 1)").Output); // ours: D205
            Assert.Equal("2", lisp.Run("(vlax-safearray-get-u-bound g 1)").Output); // ours: D205
            Assert.Equal("0", lisp.Run("(vlax-safearray-get-l-bound g 2)").Output); // AutoCAD 2021: 0 (a3)
            Assert.Equal("2", lisp.Run("(vlax-safearray-get-u-bound g 2)").Output); // AutoCAD 2021: 2 (a3)
            Assert.Equal(
                "; error: ActiveX Server returned an error: Invalid index",
                lisp.Run("(vlax-safearray-get-l-bound g 3)").Output
            ); // AutoCAD 2021 (a3)
            Assert.Equal(
                "; error: ActiveX Server returned an error: Invalid index",
                lisp.Run("(vlax-safearray-get-u-bound g 0)").Output
            ); // ours: D205, dimensions count from 1
            Assert.Equal("7", lisp.Run("(vlax-safearray-put-element g 2 1 7)").Output); // AutoCAD 2021: 7 (a3)
            Assert.Equal("7.0", lisp.Run("(vlax-safearray-get-element g 2 1)").Output); // AutoCAD 2021: 7.0, stored as a double (a3)
            Assert.Equal(
                "; error: safearray-get-element failed. Wrong count of indexes: 1",
                lisp.Run("(vlax-safearray-get-element g 2)").Output
            ); // AutoCAD 2021 (a3)
            Assert.Equal(
                "; error: safearray-get-element failed. Wrong count of indexes: 3",
                lisp.Run("(vlax-safearray-get-element g 2 1 0)").Output
            ); // ours: D205
            Assert.Equal(
                "; error: safearray-put-element failed. Wrong count of indexes: 1",
                lisp.Run("(vlax-safearray-put-element g 2 7)").Output
            ); // ours: D205
            Assert.Equal("((0.0 0.0 0.0) (0.0 7.0 0.0))", lisp.Run("(vlax-safearray->list g)").Output); // AutoCAD 2021 (a3)
        }

        [Fact]
        public void ASafearrayOfThreeDimensions_IsListedByDimension()
        {
            LispInterpreter lisp = Loaded();
            lisp.Run("(setq c (vlax-make-safearray vlax-vbInteger '(0 . 1) '(0 . 1) '(0 . 1)))");
            lisp.Run("(vlax-safearray-fill c '(((1 2) (3 4)) ((5 6) (7 8))))");
            Assert.Equal("(((1 2) (3 4)) ((5 6) (7 8)))", lisp.Run("(vlax-safearray->list c)").Output); // ours: D205, the last index runs fastest
            Assert.Equal("6", lisp.Run("(vlax-safearray-get-element c 1 0 1)").Output); // ours: D205
            Assert.Equal("3", lisp.Run("(vlax-safearray-get-dim c)").Output);
        }

        [Theory]
        [InlineData("(vlax-safearray->list 5)", "; error: bad argument type: safearrayp 5")] // AutoCAD 2021 (a2)
        [InlineData("(vlax-safearray-type 5)", "; error: bad argument type: safearrayp 5")] // AutoCAD 2021 (a3)
        [InlineData("(vlax-safearray-get-dim 5)", "; error: bad argument type: safearrayp 5")] // ours: the same check
        [InlineData("(vlax-safearray-get-l-bound nil 1)", "; error: bad argument type: safearrayp nil")] // ours: the same check
        [InlineData("(vlax-safearray-get-u-bound 5 1)", "; error: bad argument type: safearrayp 5")] // ours: the same check
        [InlineData("(vlax-safearray-get-element \"a\" 0)", "; error: bad argument type: safearrayp \"a\"")] // ours: the same check
        [InlineData("(vlax-safearray-put-element 5 0 1.0)", "; error: bad argument type: safearrayp 5")] // ours: the same check
        [InlineData(
            "(vlax-safearray->list (vlax-3d-point 1 2 3))",
            "; error: bad argument type: safearrayp #<variant 8197 ...>"
        )] // ours: a variant is no safearray
        [InlineData("(safearray-value 5)", "; error: bad argument type: safearrayp 5")] // AutoCAD 2021: the same function (a2)
        [InlineData(
            "(vlax-safearray-get-l-bound (vlax-make-safearray 5 '(0 . 1)) 1.0)",
            "; error: bad argument type: fixnump: 1.0"
        )] // ours: the dimension is an integer
        [InlineData(
            "(vlax-safearray-get-element (vlax-make-safearray 5 '(0 . 1)) 0.0)",
            "; error: bad argument type: fixnump: 0.0"
        )] // ours: an index is an integer
        [InlineData("(vlax-safearray-get-element (vlax-make-safearray 5 '(0 . 1)))", "; error: too few arguments")] // ours: L1 D16
        [InlineData("(vlax-safearray-put-element (vlax-make-safearray 5 '(0 . 1)) 0)", "; error: too few arguments")] // ours: L1 D16
        public void ASafearrayFunctionOnAnotherValue_GivesAnError(string expression, string output)
        {
            Assert.Equal(output, Out(expression));
        }

        [Theory]
        [InlineData(
            "(vlax-safearray-put-element (vlax-make-safearray 5 '(0 . 1)) 0 \"a\")",
            "; error: lisp value has no coercion to VARIANT with this type:  \"a\""
        )] // ours: D205, as the fill
        [InlineData(
            "(vlax-safearray-put-element (vlax-make-safearray 5 '(0 . 1)) 0 nil)",
            "; error: lisp value has no coercion to VARIANT with this type:  nil"
        )] // ours: D205, nil is no number
        [InlineData(
            "(vlax-safearray-put-element (vlax-make-safearray 5 '(0 . 1)) 0 '(1))",
            "; error: lisp value has no coercion to VARIANT with this type:  (1)"
        )] // ours: D205
        [InlineData("(vlax-safearray-put-element (vlax-make-safearray 8 '(0 . 1)) 0 12)", "12")] // ours: D205, gives the value as given
        [InlineData(
            "(vlax-safearray->list (progn (setq a (vlax-make-safearray 8 '(0 . 1))) (vlax-safearray-put-element a 1 12) a))",
            "(\"\" \"12\")"
        )] // ours: D205, the text of a number
        [InlineData(
            "(vlax-safearray->list (progn (setq a (vlax-make-safearray 11 '(0 . 1))) (vlax-safearray-put-element a 1 T) a))",
            "(:vlax-false :vlax-true)"
        )] // ours: D205
        [InlineData(
            "(vlax-safearray->list (progn (setq a (vlax-make-safearray 12 '(0 . 1))) (vlax-safearray-put-element a 0 \"x\") a))",
            "(#<variant 8 x> #<variant 0 >)"
        )] // ours: D205, a variant array takes any value
        [InlineData(
            "(vlax-safearray-get-element (progn (setq a (vlax-make-safearray 12 '(0 . 1))) (vlax-safearray-put-element a 0 \"x\") a) 0)",
            "#<variant 8 x>"
        )] // ours: D205
        [InlineData("(vlax-safearray-get-element (vlax-make-safearray 11 '(0 . 1)) 0)", ":vlax-false")] // ours: D205, a Boolean reads as the symbol
        [InlineData("(vlax-safearray-get-element (vlax-make-safearray 9 '(0 . 1)) 0)", "nil")] // ours: D205
        public void PuttingAnElement_ConvertsItToTheElementType(string expression, string output)
        {
            Assert.Equal(output, Out(expression));
        }

        [Fact]
        public void ASafearrayWithNoElement_ListsAsAnInvalidIndex()
        {
            LispInterpreter lisp = Loaded();
            lisp.SetValue("empty", LispSafeArray.Objects(new LispVlaObject[0]));
            Assert.Equal("safearray", lisp.Run("(type empty)").Output);
            Assert.Equal(
                "; error: ActiveX Server returned an error: Invalid index",
                lisp.Run("(vlax-safearray->list empty)").Output
            ); // AutoCAD 2021 (GUI g2): the list of an empty GetAttributes array (D244)
            Assert.Equal("-1", lisp.Run("(vlax-safearray-get-u-bound empty 1)").Output); // ours: D205, the bounds 0 and -1
            Assert.Equal("9", lisp.Run("(vlax-safearray-type empty)").Output);
            Assert.Equal(
                "; error: ActiveX Server returned an error: Invalid index",
                lisp.Run("(vlax-safearray-get-element empty 0)").Output
            ); // ours: D205
        }

        [Fact]
        public void AnObjectArray_HoldsObjectsAndTheirVariantIsOfType8201()
        {
            LispInterpreter lisp = Loaded();
            var one = new LispVlaObject(LispVlaKind.Entity, "IAcadLine", "2A");
            lisp.SetValue("o", one);
            lisp.Run("(setq a (vlax-make-safearray vlax-vbObject '(0 . 1)))");
            Assert.Equal("#<safearray...>", lisp.Run("(vlax-safearray-fill a (list o nil))").Output); // ours: D205
            Assert.Equal(one.PrintForm, lisp.Run("(vlax-safearray-get-element a 0)").Output);
            Assert.Equal("nil", lisp.Run("(vlax-safearray-get-element a 1)").Output);
            Assert.Equal("8201", lisp.Run("(vlax-variant-type (vlax-make-variant a))").Output); // ours: D204, 8192 plus the element type
            Assert.Equal("#<variant 9 ...>", lisp.Run("(vlax-make-variant o)").Output); // ours: D203, an object prints with ... as an array does
            Assert.Equal("9", lisp.Run("(vlax-variant-type (vlax-make-variant o))").Output); // ours: D204
            Assert.Equal(one.PrintForm, lisp.Run("(vlax-variant-value (vlax-make-variant o))").Output); // ours: D204, 9 gives the object
            Assert.Equal("T", lisp.Run("(eq a (vlax-variant-value (vlax-make-variant a)))").Output); // ours: D204, the array itself
        }

        [Theory]
        [InlineData("(vlax-variant-type (vlax-make-variant (vlax-make-safearray vlax-vbString '(0 . 1))))", "8200")] // AutoCAD 2021: 8200 (a3)
        [InlineData("(vlax-variant-type (vlax-make-variant (vlax-make-safearray vlax-vbVariant '(0 . 1))))", "8204")] // AutoCAD 2021: 8204 (a3)
        [InlineData("(vlax-make-variant (vlax-make-safearray vlax-vbString '(0 . 1)))", "#<variant 8200 ...>")] // AutoCAD 2021: #<variant 8200 ...> (a3)
        [InlineData("(vlax-variant-type (vlax-make-variant (vlax-make-safearray vlax-vbDouble '(0 . 1))))", "8197")] // ours: D204, 8192 plus 5
        [InlineData(
            "(vlax-variant-type (vlax-make-variant (vlax-make-safearray vlax-vbDouble '(0 . 1)) 8197))",
            "8197"
        )] // ours: D204, its own type given
        [InlineData(
            "(vlax-make-variant (vlax-make-safearray vlax-vbDouble '(0 . 1)) 8200)",
            "; error: lisp value has no coercion to VARIANT with this type:  #<safearray...>"
        )] // ours: D204, another type
        [InlineData(
            "(vlax-make-variant (vlax-make-safearray vlax-vbDouble '(0 . 1)) vlax-vbDouble)",
            "; error: lisp value has no coercion to VARIANT with this type:  #<safearray...>"
        )] // ours: D204, an array is not a double
        [InlineData(
            "(vlax-variant-value (vlax-make-variant (vlax-make-safearray vlax-vbDouble '(0 . 1))))",
            "#<safearray...>"
        )] // ours: D204, 8192 and over give the array
        public void AVariantOfAnArray_HasTheArrayTypeOfItsElements(string expression, string output)
        {
            Assert.Equal(output, Out(expression));
        }

        [Theory]
        [InlineData("(vlax-make-variant 3.7 vlax-vbSingle)", "#<variant 4 3.7>")] // ours: D204, a single prints with 7 digits
        [InlineData("(vlax-variant-value (vlax-make-variant 3.7 vlax-vbSingle))", "3.7")] // ours: D204, a real of the single
        [InlineData("(vlax-make-variant 0.1 vlax-vbSingle)", "#<variant 4 0.1>")] // ours: D204
        [InlineData(
            "(vlax-make-variant 1e39 vlax-vbSingle)",
            "; error: lisp value has no coercion to VARIANT with this type:  1.0e+39"
        )] // ours: D204, past a single
        public void ASingle_KeepsSevenDigits(string expression, string output)
        {
            Assert.Equal(output, Out(expression));
        }

        #endregion
    }
}
