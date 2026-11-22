using System;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// <c>regapp</c>, <c>xdroom</c>, <c>xdsize</c> and the extended data of <c>entmod</c> and <c>entmake</c>, with the rows
    /// AutoCAD 2021's accoreconsole gave (probes x1, x2 and x3 of the C4b measurements, kept in the transcripts of the lispux
    /// scratch). An entity name and a handle are written <c>*</c>, as the harness normalises them.
    /// </summary>
    public sealed class LispXdataTests : IDisposable
    {
        // Ends a routine a test left waiting, so no worker thread outlives its test.
        public void Dispose() => WaitingRoutines.EndAll();

        private static string Value(CadCommandSession session, string form) => LispEntityTests.Value(session, form);

        // A drawing with the applications APPA, APPB and APPC registered and a line of 5 by 5 named e.
        private static CadCommandSession Registered()
        {
            CadCommandSession session = Session(Blank());
            Value(session, "(setvar \"CMDECHO\" 0)");
            Value(session, "(regapp \"APPA\")");
            Value(session, "(regapp \"APPB\")");
            Value(session, "(regapp \"APPC\")");
            Value(session, "(setq e (progn (entmake '((0 . \"LINE\")(10 0.0 0.0 0.0)(11 5.0 5.0 0.0))) (entlast)))");
            return session;
        }

        private static string Applications(CadCommandSession session, string names = "\"*\"") =>
            Value(session, "(cdr (assoc -3 (entget e '(" + names + "))))");

        #region regapp

        [Fact]
        public void RegappOfANewName_GivesTheNameAndListsItInTheTableOfApplications()
        {
            CadCommandSession session = Session(Blank());

            // AutoCAD 2021 (x1): (regapp "GHAPP1") is "GHAPP1" and tblsearch then finds ((0 . "APPID") (2 . "GHAPP1") (70 . 0)).
            Assert.Equal("\"GHAPP1\"", Value(session, "(regapp \"GHAPP1\")"));
            Assert.Equal(
                "((0 . \"APPID\") (2 . \"GHAPP1\") (70 . 0))",
                Value(session, "(tblsearch \"APPID\" \"GHAPP1\")")
            );
        }

        [Theory]
        [InlineData("(regapp \"GHAPP1\")")]
        [InlineData("(regapp \"ghapp1\")")]
        [InlineData("(regapp \"ACAD\")")]
        [InlineData("(regapp \"acad\")")]
        public void RegappOfAKnownName_GivesNil_WhateverTheCaseOfTheLetters(string again)
        {
            CadCommandSession session = Session(Blank());
            Value(session, "(regapp \"GHAPP1\")");

            // AutoCAD 2021 (x1): the name a second time, in either case, and ACAD, which every drawing has, give nil.
            Assert.Equal("nil", Value(session, again));
        }

        [Theory]
        [InlineData("")]
        [InlineData("bad<name")]
        [InlineData("a;b")]
        [InlineData("a,b")]
        public void RegappOfANameATableRefuses_GivesNil(string name)
        {
            CadCommandSession session = Session(Blank());

            // AutoCAD 2021 (x1): "", "bad<name", "a;b" and "a,b" are nil; "has space" is registered.
            Assert.Equal("nil", Value(session, "(regapp \"" + name + "\")"));
            Assert.Equal("\"has space\"", Value(session, "(regapp \"has space\")"));
        }

        [Theory]
        [InlineData("(regapp 5)", "; error: bad argument type: stringp 5")]
        [InlineData("(regapp nil)", "; error: bad argument type: stringp nil")]
        [InlineData("(regapp 'sym)", "; error: bad argument type: stringp SYM")]
        [InlineData("(regapp)", "; error: too few arguments")]
        [InlineData("(regapp \"A\" \"B\")", "; error: too many arguments")]
        public void RegappWithTheWrongArguments_GivesAutoCadsErrors(string form, string error)
        {
            Assert.Equal(error, Value(Session(Blank()), form));
        }

        #endregion

        #region xdroom

        [Fact]
        public void XdroomOfAnEntityWithNoData_Is16383()
        {
            CadCommandSession session = Registered();

            // AutoCAD 2021 (x1): (xdroom e) of a new line is 16383, and so is that of a table record and of the root dictionary.
            Assert.Equal("16383", Value(session, "(xdroom e)"));
            Assert.Equal("16383", Value(session, "(xdroom (tblobjname \"APPID\" \"APPA\"))"));
            Assert.Equal("16383", Value(session, "(xdroom (namedobjdict))"));
        }

        [Theory]
        [InlineData("(1000 . \"abc\")", 16371)]
        [InlineData("(1000 . \"abc\") (1070 . 5)", 16368)]
        [InlineData("(1000 . \"\") (1000 . \"a\") (1000 . \"abcdefghij\")", 16349)]
        [InlineData("(1002 . \"{\") (1070 . 1) (1002 . \"}\")", 16373)]
        [InlineData("(1070 . 1)", 16377)]
        [InlineData("(1071 . 100000)", 16375)]
        [InlineData("(1040 . 1.5)", 16371)]
        [InlineData("(1041 . 1.5)", 16371)]
        [InlineData("(1042 . 1.5)", 16371)]
        [InlineData("(1010 1.0 2.0 3.0)", 16355)]
        [InlineData("(1011 1.0 2.0 3.0)", 16355)]
        [InlineData("(1012 1.0 2.0 3.0)", 16355)]
        [InlineData("(1013 1.0 2.0 3.0)", 16355)]
        [InlineData("(1003 . \"0\")", 16377)]
        [InlineData("(1004 . \"0A0B\")", 16376)]
        public void XdroomAfterAnEntmod_IsTheLimitLessTheBytesOfWhatItHolds(string groups, int room)
        {
            CadCommandSession session = Registered();

            // AutoCAD 2021 (x1): an application with a text of 3 letters is 12 bytes (3 for its name, 3 and 2 a letter for the text),
            // an integer 3 more than its name, a long 5, a real 9, a point 25, a brace 2, a layer's name 3, 2 and a byte
            // for each two digits of binary data, a text 3 and 2 for each letter.
            Value(session, "(entmod (append (entget e) '((-3 (\"APPA\" " + groups + ")))))");

            Assert.Equal(room.ToString(), Value(session, "(xdroom e)"));
        }

        [Fact]
        public void XdroomOfALayerWithData_CountsItAsOfAnEntity()
        {
            CadCommandSession session = Registered();
            Value(session, "(setq e (tblobjname \"LAYER\" \"0\"))");

            // AutoCAD 2021 (x3): a layer with a text of 3 letters under one application has room for 16371. The presenter's entmod
            // does not change a record of a table yet, so the data goes in by the object, and xdroom reads it.
            CadDocument document = session.Document;
            document.Database.LayerTable["0"].XData = new[]
            {
                new DxfTypedValue(1001, "APPA"),
                new DxfTypedValue(1000, "lay"),
            };

            Assert.Equal("16371", Value(session, "(xdroom e)"));
        }

        [Theory]
        [InlineData("(xdroom)", "; error: too few arguments")]
        [InlineData("(xdroom 5)", "; error: bad argument type: lentityp 5")]
        [InlineData("(xdroom nil)", "; error: bad argument type: lentityp nil")]
        [InlineData("(xdroom e e)", "; error: too many arguments")]
        public void XdroomWithTheWrongArguments_GivesAutoCadsErrors(string form, string error)
        {
            Assert.Equal(error, Value(Registered(), form));
        }

        #endregion

        #region xdsize

        [Theory]
        [InlineData("((\"APPA\" (1000 . \"abc\")))", "12")]
        [InlineData("((\"APPA\" (1000 . \"abc\")) (\"APPB\" (1000 . \"abc\")))", "24")]
        [InlineData("((\"APPA\" (1000 . \"abc\")) (\"APPA\" (1000 . \"abc\")))", "12")]
        [InlineData("((\"appa\" (1000 . \"abc\")))", "12")]
        [InlineData("((\"APPA\"))", "0")]
        [InlineData("((\"APPA\" (1000 . \"abc\")) (\"APPB\"))", "12")]
        [InlineData("((\"APPA\" (1005 . \"155\")))", "12")]
        [InlineData("((\"APPA\" (1002 . \"{\")))", "5")]
        [InlineData("((\"APPA\" (1002 . \"{\") (1070 . 1) (1002 . \"}\")))", "10")]
        [InlineData("((\"APPA\" (1070 . 5) (1070 . 6)))", "9")]
        [InlineData("((\"APPA\" (1040 . 1)))", "12")]
        [InlineData("((\"APPA\" (1071 . 1.5)))", "8")]
        [InlineData("((\"APPA\" (1010 1.0 2.0)))", "28")]
        [InlineData("((\"APPA\" (1010 1 2 3)))", "28")]
        [InlineData("((\"APPA\" (1004 . \"0A0\")))", "6")]
        [InlineData("((\"APPA\" (1003 . \"0\")))", "6")]
        [InlineData("((\"APPA\" (1001 . \"APPB\")))", "3")]
        public void XdsizeOfAList_CountsTheBytesAutoCadCounts(string list, string size)
        {
            // AutoCAD 2021 (x1, x2, x3): the list is the value of a group -3; an application with no data takes none, one that is
            // there twice is counted once, and a text of letters beyond ASCII counts two bytes for each.
            Assert.Equal(size, Value(Registered(), "(xdsize '" + list + ")"));
        }

        [Fact]
        public void XdsizeOfATextWithLettersBeyondAscii_CountsTwoBytesForEachLetter()
        {
            // AutoCAD 2021 (x2): (xdsize '(("APPA" (1000 . "caf\U+00E9")))) is 14 and the two Chinese letters are 10.
            Assert.Equal("14", Value(Registered(), "(xdsize (list (list \"APPA\" (cons 1000 \"café\"))))"));
            Assert.Equal("10", Value(Registered(), "(xdsize (list (list \"APPA\" (cons 1000 \"中文\"))))"));
        }

        [Theory]
        [InlineData("((\"NOPE\" (1000 . \"abc\")))")]
        [InlineData("((\"APPA\" (1000 . \"abc\")) (\"NOPE\" (1000 . \"abc\")))")]
        [InlineData("((\"\" (1000 . \"abc\")))")]
        [InlineData("((\"APPA\" (1003 . \"NOLAYER\")))")]
        public void XdsizeOfAnApplicationNotRegisteredOrALayerNotThere_IsNil(string list)
        {
            Assert.Equal("nil", Value(Registered(), "(xdsize '" + list + ")"));
        }

        [Theory]
        [InlineData("((1001 . \"APPA\") (1000 . \"abc\"))")]
        [InlineData("((-3 (\"APPA\" (1000 . \"abc\"))))")]
        [InlineData("((\"APPA\" 5))")]
        [InlineData("((\"APPA\" (1070 . 1.5)))")]
        [InlineData("((\"APPA\" (1000 . 5)))")]
        [InlineData("((\"APPA\" (5 . \"abc\")))")]
        [InlineData("((\"APPA\" (10 1.0 2.0 3.0)))")]
        [InlineData("((\"APPA\" (1010 . 1.0)))")]
        [InlineData("((\"APPA\" (1010 1.0 2.0 3.0 4.0)))")]
        [InlineData("((\"APPA\" (1099 . \"abc\")))")]
        [InlineData("(5)")]
        [InlineData("((nil (1000 . \"abc\")))")]
        public void XdsizeOfAListThatIsNoExtendedData_IsBadXdataList(string list)
        {
            // AutoCAD 2021 (x1, x3): the error names the list as it was given. Four numbers for a point crashed AutoCAD's console
            // (probe x2, 1 crash, no report window left); the presenter says bad xdata list.
            string quoted = list.Replace("\\\"", "\"");
            Assert.StartsWith("; error: bad xdata list: ", Value(Registered(), "(xdsize '" + quoted + ")"));
        }

        [Theory]
        [InlineData("(xdsize nil)", "; error: bad xdata list: nil")]
        [InlineData("(xdsize 5)", "; error: bad xdata list: 5")]
        [InlineData("(xdsize \"a\")", "; error: bad xdata list: \"a\"")]
        [InlineData("(xdsize)", "; error: too few arguments")]
        [InlineData("(xdsize '(1) '(2))", "; error: too many arguments")]
        [InlineData("(xdsize '((\"APPA\" (1004 . \"ZZ\"))))", "; error: invalid character in 1004 group")]
        public void XdsizeWithTheWrongArguments_GivesAutoCadsErrors(string form, string error)
        {
            Assert.Equal(error, Value(Registered(), form));
        }

        #endregion

        #region entmod and entmake with a group -3

        [Fact]
        public void EntmodWithAnApplicationNotRegistered_WritesTheRefusalAndGivesNil()
        {
            CadCommandSession session = Registered();

            // AutoCAD 2021 (x1): "Invalid application name in 1001 group" and nil; the entity keeps what it had.
            Assert.Equal(
                "Invalid application name in 1001 groupnil",
                Value(session, "(entmod (append (entget e) '((-3 (\"GHAPP9\" (1000 . \"abc\"))))))")
            );
            Assert.Equal("nil", Applications(session, "\"GHAPP9\""));
            Assert.Equal("nil", Applications(session));
        }

        [Fact]
        public void EntmodWithTheApplicationNotRegisteredAmongOthers_ChangesNothing()
        {
            CadCommandSession session = Registered();
            Value(session, "(entmod (append (entget e) '((-3 (\"APPA\" (1000 . \"x\") (1070 . 1))))))");

            // AutoCAD 2021 (x2): one application not in the table refuses the whole group.
            Assert.Equal(
                "Invalid application name in 1001 groupnil",
                Value(
                    session,
                    "(entmod (append (entget e) '((-3 (\"APPA\" (1000 . \"y\")) (\"NOPE\" (1000 . \"z\"))))))"
                )
            );
            Assert.Equal("((\"APPA\" (1000 . \"x\") (1070 . 1)))", Applications(session));
        }

        [Fact]
        public void EntmakeWithAnApplicationNotRegistered_MakesNothing()
        {
            CadCommandSession session = Registered();
            Value(session, "(setq before (entlast))");

            // AutoCAD 2021 (x1): the refusal and nil, and (entlast) is the line made before.
            Assert.Equal(
                "Invalid application name in 1001 groupnil",
                Value(
                    session,
                    "(entmake '((0 . \"LINE\")(10 0.0 0.0 0.0)(11 5.0 5.0 0.0)(-3 (\"GHAPP7\" (1000 . \"made\")))))"
                )
            );
            Assert.Equal("T", Value(session, "(eq before (entlast))"));
        }

        [Fact]
        public void EntmakeWithTheDataOfAnApplication_GivesTheEntityThatData()
        {
            CadCommandSession session = Registered();

            // AutoCAD 2021 (x1): the list is given back as it was, and the line has the data.
            Assert.Equal(
                "((0 . \"LINE\") (10 0.0 0.0 0.0) (11 5.0 5.0 0.0) (-3 (\"APPA\" (1000 . \"made\"))))",
                Value(
                    session,
                    "(entmake '((0 . \"LINE\")(10 0.0 0.0 0.0)(11 5.0 5.0 0.0)(-3 (\"APPA\" (1000 . \"made\")))))"
                )
            );
            Assert.Equal(
                "((\"APPA\" (1000 . \"made\")))",
                Value(session, "(cdr (assoc -3 (entget (entlast) '(\"*\"))))")
            );
        }

        [Fact]
        public void EntmakeWithTwoGroupsMinus3_KeepsTheApplicationsOfTheLastAlone()
        {
            CadCommandSession session = Registered();

            // AutoCAD 2021 (x1): with (-3 ("GHAPP1" ...)) and (-3 ("GHAPP2" ...)) the line has the data of GHAPP2 only.
            Value(
                session,
                "(entmake '((0 . \"LINE\")(10 0.0 0.0 0.0)(11 5.0 5.0 0.0)(-3 (\"APPA\" (1000 . \"made\")))(-3 (\"APPB\" (1000 . \"m2\")))))"
            );

            Assert.Equal(
                "((\"APPB\" (1000 . \"m2\")))",
                Value(session, "(cdr (assoc -3 (entget (entlast) '(\"*\"))))")
            );
        }

        [Fact]
        public void EntmakeWithAnApplicationAndNoData_MakesTheEntityWithNone()
        {
            CadCommandSession session = Registered();

            // AutoCAD 2021 (x3): (-3) and (-3 ("APPA")) are given back as they were and the line has no data.
            Assert.Equal(
                "((0 . \"LINE\") (10 0.0 0.0 0.0) (11 5.0 5.0 0.0) (-3 (\"APPA\")))",
                Value(session, "(entmake '((0 . \"LINE\")(10 0.0 0.0 0.0)(11 5.0 5.0 0.0)(-3 (\"APPA\"))))")
            );
            Assert.Equal("nil", Value(session, "(assoc -3 (entget (entlast) '(\"*\")))"));
        }

        [Fact]
        public void EntmodGivesTheDataWrittenLastFirst_ThoseNotNamedKeepingTheirOrder()
        {
            CadCommandSession session = Registered();
            Value(session, "(setq ent (entget e))");

            // AutoCAD 2021 (x2): written B, A, C, then B again, entget "*" gives B, C, A; each time the application written
            // is first and the others follow in the order they had.
            Value(session, "(entmod (append ent '((-3 (\"APPB\" (1000 . \"b\"))))))");
            Assert.Equal("((\"APPB\" (1000 . \"b\")))", Applications(session));
            Value(session, "(entmod (append ent '((-3 (\"APPA\" (1000 . \"a\"))))))");
            Assert.Equal("((\"APPA\" (1000 . \"a\")) (\"APPB\" (1000 . \"b\")))", Applications(session));
            Value(session, "(entmod (append ent '((-3 (\"APPC\" (1000 . \"c\"))))))");
            Assert.Equal(
                "((\"APPC\" (1000 . \"c\")) (\"APPA\" (1000 . \"a\")) (\"APPB\" (1000 . \"b\")))",
                Applications(session)
            );
            Value(session, "(entmod (append ent '((-3 (\"APPB\" (1000 . \"b2\"))))))");
            Assert.Equal(
                "((\"APPB\" (1000 . \"b2\")) (\"APPC\" (1000 . \"c\")) (\"APPA\" (1000 . \"a\")))",
                Applications(session)
            );
        }

        [Fact]
        public void EntgetWithNames_GivesThoseApplicationsInTheOrderTheEntityHasThem()
        {
            CadCommandSession session = Registered();
            Value(session, "(setq ent (entget e))");
            Value(session, "(entmod (append ent '((-3 (\"APPB\" (1000 . \"b\"))))))");
            Value(session, "(entmod (append ent '((-3 (\"APPA\" (1000 . \"a\"))))))");
            Value(session, "(entmod (append ent '((-3 (\"APPC\" (1000 . \"c\"))))))");

            // AutoCAD 2021 (x2): '("APPA" "APPC") gives C then A; '("appa") finds APPA; an unknown name adds nothing; '("*" "APPA")
            // gives all of them.
            Assert.Equal(
                "((\"APPC\" (1000 . \"c\")) (\"APPA\" (1000 . \"a\")))",
                Applications(session, "\"APPA\" \"APPC\"")
            );
            Assert.Equal("((\"APPA\" (1000 . \"a\")))", Applications(session, "\"appa\""));
            Assert.Equal("nil", Applications(session, "\"NOPE\""));
            Assert.Equal(
                "((\"APPC\" (1000 . \"c\")) (\"APPA\" (1000 . \"a\")) (\"APPB\" (1000 . \"b\")))",
                Applications(session, "\"*\" \"APPA\"")
            );
        }

        [Theory]
        [InlineData("(entget e '(5))", "; error: not a string in the list: 5")]
        [InlineData("(entget e \"APPA\")", "; error: bad list (or tail): \"APPA\"")]
        public void EntgetWithAnArgumentThatIsNoListOfNames_GivesAutoCadsErrors(string form, string error)
        {
            Assert.Equal(error, Value(Registered(), form));
        }

        [Fact]
        public void EntmodOfAnApplicationNamedAlone_TakesItsDataOutAndLeavesTheOthers()
        {
            CadCommandSession session = Registered();
            Value(session, "(setq ent (entget e))");
            Value(session, "(entmod (append ent '((-3 (\"APPA\" (1000 . \"a\"))))))");
            Value(session, "(entmod (append ent '((-3 (\"APPB\" (1000 . \"b\"))))))");

            // AutoCAD 2021 (x1): (-3 ("GHAPP2")) gives the list back and the application has no data after it.
            Assert.Equal("(-3 (\"APPA\"))", Value(session, "(last (entmod (append ent '((-3 (\"APPA\"))))))"));
            Assert.Equal("((\"APPB\" (1000 . \"b\")))", Applications(session));
        }

        [Fact]
        public void EntmodWithTwoGroupsMinus3_WritesTheLastAndTakesTheDataOfEveryApplicationNamed()
        {
            CadCommandSession session = Registered();
            Value(session, "(setq ent (entget e))");
            Value(
                session,
                "(entmod (append ent '((-3 (\"APPA\" (1000 . \"x\")) (\"APPB\" (1000 . \"y\")) (\"APPC\" (1000 . \"c\"))))))"
            );

            // AutoCAD 2021 (x2): with (-3 ("APPA" ...)) and (-3 ("APPB" ...)) the line has B as given and C as it was, and no A.
            Value(session, "(entmod (append ent '((-3 (\"APPA\" (1000 . \"q\"))) (-3 (\"APPB\" (1000 . \"r\"))))))");

            Assert.Equal("((\"APPB\" (1000 . \"r\")) (\"APPC\" (1000 . \"c\")))", Applications(session));
        }

        [Theory]
        [InlineData("(-3 (\"APPA\" (1070 . \"s\")))", "; error: bad DXF group: (-3 (\"APPA\" (1070 . \"s\")))")]
        [InlineData("(-3 (\"APPA\" (1000 . 5)))", "; error: bad DXF group: (-3 (\"APPA\" (1000 . 5)))")]
        [InlineData("(-3 (\"APPA\" (1070 . 5.5)))", "; error: bad DXF group: (-3 (\"APPA\" (1070 . 5.5)))")]
        [InlineData("(-3 nil)", "; error: bad DXF group: (-3 nil)")]
        [InlineData("(-3 (\"APPA\" (1099 . 5)))", "Unrecognized field typenil")]
        [InlineData("(-3 (\"APPA\" (1002 . \"{\")))", "Improper nesting in application data.nil")]
        [InlineData("(-3 (\"APPA\" (1002 . \"}\")))", "Improper nesting in application data.nil")]
        public void EntmodWithAGroup3AutoCadRefuses_GivesItsErrorOrItsLine(string group, string said)
        {
            CadCommandSession session = Registered();

            // AutoCAD 2021 (x2, x3): a value of the wrong kind is the error with the whole group; a code of no meaning, and a brace
            // opened and not shut or shut and not opened, write a line and give nil.
            Assert.Equal(said, Value(session, "(entmod (append (entget e) '(" + group + ")))"));
        }

        [Fact]
        public void EntmodWithBracesInsideBraces_KeepsThemAndCountsTheirBytes()
        {
            CadCommandSession session = Registered();

            // AutoCAD 2021 (x2): "{" "{" "}" "}" is accepted and takes 3 + 4 * 2 = 11 bytes.
            Value(
                session,
                "(entmod (append (entget e) '((-3 (\"APPA\" (1002 . \"{\") (1002 . \"{\") (1002 . \"}\") (1002 . \"}\"))))))"
            );

            Assert.Equal("16372", Value(session, "(xdroom e)"));
        }

        [Fact]
        public void EntmodWithPointsAndNumbers_KeepsThemAsRealsWithThreeCoordinates()
        {
            CadCommandSession session = Registered();

            // AutoCAD 2021 (x2): (1010 1 2 3) is (1010 1.0 2.0 3.0) afterwards and (1040 . 5) is (1040 . 5.0).
            Value(session, "(entmod (append (entget e) '((-3 (\"APPA\" (1010 1 2 3) (1040 . 5))))))");

            Assert.Equal("((\"APPA\" (1010 1.0 2.0 3.0) (1040 . 5.0)))", Applications(session));
        }

        [Fact]
        public void EntmodWithAHandleOfNoObject_GivesNilAndWritesNothing()
        {
            CadCommandSession session = Registered();

            // AutoCAD 2021 (x2): (1005 . "1F"), a handle no object has, is nil.
            Assert.Equal("nil", Value(session, "(entmod (append (entget e) '((-3 (\"APPA\" (1005 . \"FFFF\"))))))"));
            Assert.Equal("nil", Applications(session));
        }

        [Fact]
        public void EntmodWithTheHandleOfAnObject_KeepsTheHandle()
        {
            CadCommandSession session = Registered();
            Value(session, "(setq h (cdr (assoc 5 (entget e))))");

            Value(session, "(entmod (append (entget e) (list (list -3 (list \"APPA\" (cons 1005 h))))))");

            Assert.Equal(
                "T",
                Value(session, "(equal (cdr (assoc 1005 (cdr (car (cdr (assoc -3 (entget e '(\"*\")))))))) h)")
            );
        }

        [Fact]
        public void TheDataOfEntmod_SurvivesASaveAsDxfAndAnOpen()
        {
            CadCommandSession session = Registered();
            Value(session, "(entmod (append (entget e) '((-3 (\"APPA\" (1000 . \"abc\") (1070 . 5) (1040 . 2.5))))))");

            CadDocument opened = CadDocument.From(DxfDatabase.Parse(session.Document.Database.ToText()), "x.dxf");
            CadCommandSession again = Session(opened);

            Assert.Equal(
                "((\"APPA\" (1000 . \"abc\") (1070 . 5) (1040 . 2.5)))",
                Value(again, "(cdr (assoc -3 (entget (entlast) '(\"APPA\"))))")
            );
            Assert.Equal("16359", Value(again, "(xdroom (entlast))"));
        }

        #endregion
    }
}
