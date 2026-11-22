using System;
using System.IO;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// <c>namedobjdict</c>, <c>dictsearch</c>, <c>dictnext</c>, <c>dictadd</c>, <c>dictremove</c>, <c>dictrename</c>, and the
    /// XRECORDs and DICTIONARYs <c>entmakex</c> and <c>entmake</c> make, with the rows AutoCAD 2021's accoreconsole gave (probes
    /// d1, d2 and d3 of the C4b measurements, kept in the transcripts of the lispux scratch). An entity name and a handle are
    /// written <c>*</c>, as the harness normalises them.
    /// </summary>
    public sealed class LispDictionaryTests : IDisposable
    {
        // Ends a routine a test left waiting, so no worker thread outlives its test.
        public void Dispose() => WaitingRoutines.EndAll();

        private static string Value(CadCommandSession session, string form) => LispEntityTests.Value(session, form);

        private const string Plain = "'((0 . \"XRECORD\")(100 . \"AcDbXrecord\")(1 . \"hello\"))";

        // A drawing with a dictionary d under the name GHD in the root, holding the XRECORDs of bb, aa and cc (in that order of making).
        private static CadCommandSession WithDictionary()
        {
            CadCommandSession session = Session(Blank());
            Value(session, "(setvar \"CMDECHO\" 0)");
            Value(session, "(setq nod (namedobjdict))");
            Value(session, "(setq d (entmakex '((0 . \"DICTIONARY\")(100 . \"AcDbDictionary\"))))");
            Value(session, "(dictadd nod \"GHD\" d)");

            foreach (string name in new[] { "bb", "aa", "cc" })
            {
                Value(
                    session,
                    "(dictadd d \""
                        + name
                        + "\" (entmakex '((0 . \"XRECORD\")(100 . \"AcDbXrecord\")(1 . \""
                        + name
                        + "\"))))"
                );
            }

            return session;
        }

        private static string Names(CadCommandSession session, string dictionary)
        {
            Value(session, "(setq walk (dictnext " + dictionary + " T) names \"\")");
            Value(
                session,
                "(while walk (setq names (strcat names (cdr (assoc 1 walk)) \" \") walk (dictnext " + dictionary + ")))"
            );
            return Value(session, "(strcat names)");
        }

        #region namedobjdict and dictsearch

        [Fact]
        public void NamedobjdictGivesTheNameOfTheRootDictionary()
        {
            CadCommandSession session = Session(Blank());

            // AutoCAD 2021 (d1): (type (namedobjdict)) is ENAME, and a second argument is too many.
            Assert.Equal("ENAME", Value(session, "(type (namedobjdict))"));
            Assert.Equal("; error: too many arguments", Value(session, "(namedobjdict 1)"));
        }

        [Fact]
        public void EntgetOfTheRootDictionary_ListsItsEntriesBy3And350WithNoOwner()
        {
            CadCommandSession session = Session(Blank());

            // AutoCAD 2021 (d1): ((-1 . E) (0 . "DICTIONARY") (330 . <Entity name: 0>) (5 . "C") (100 . "AcDbDictionary") (280 . 0)
            // (281 . 1) (3 . "ACAD_CIP_PREVIOUS_PRODUCT_INFO") (350 . E) ...): the owner is the name 0, the flags are listed
            // where the file has not 280, and each entry is its name and the object.
            string listed = Value(session, "(entget (namedobjdict))");

            Assert.StartsWith(
                "((-1 . <Entity name: *>) (0 . \"DICTIONARY\") (330 . <Entity name: *>) (5 . \"*\") (100 . \"AcDbDictionary\") (280 . 0) (281 . 1) (3 . \"ACAD_GROUP\") (350 . <Entity name: *>)",
                listed
            );
            Assert.Equal(
                "T",
                Value(
                    session,
                    "(if (wcmatch (vl-princ-to-string (cdr (assoc 330 (entget (namedobjdict))))) \"*: 0>\") T)"
                )
            );
        }

        [Theory]
        [InlineData("(dictsearch (namedobjdict) \"ACAD_GROUP\")")]
        [InlineData("(dictsearch (namedobjdict) \"acad_group\")")]
        [InlineData("(dictsearch (namedobjdict) \"ACAD_GROUP\" T)")]
        public void DictsearchOfAName_GivesTheListOfItsObject_LettersOfEitherCaseTheSame(string form)
        {
            CadCommandSession session = Session(Blank());

            // AutoCAD 2021 (d1): ((-1 . E) (0 . "DICTIONARY") (330 . E) (5 . "D") (100 . "AcDbDictionary") (280 . 0) (281 . 1)).
            Assert.Equal(
                "((-1 . <Entity name: *>) (0 . \"DICTIONARY\") (330 . <Entity name: *>) (5 . \"*\") (100 . \"AcDbDictionary\") (280 . 0) (281 . 1))",
                Value(session, form)
            );
        }

        [Theory]
        [InlineData("(dictsearch (namedobjdict) \"NOPE\")", "nil")]
        [InlineData("(dictsearch (namedobjdict) \"\")", "nil")]
        [InlineData("(dictsearch (namedobjdict) \" ACAD_GROUP \")", "nil")]
        [InlineData("(dictsearch (namedobjdict) 5)", "; error: bad argument type: stringp 5")]
        [InlineData("(dictsearch 5 \"a\")", "; error: bad argument type: lentityp 5")]
        [InlineData("(dictsearch nil \"a\")", "; error: bad argument type: lentityp nil")]
        [InlineData("(dictsearch (namedobjdict))", "; error: too few arguments")]
        [InlineData("(dictsearch (namedobjdict) \"a\" nil nil)", "; error: too many arguments")]
        public void DictsearchOfWhatIsNotThere_GivesNilOrAutoCadsError(string form, string expected)
        {
            Assert.Equal(expected, Value(Session(Blank()), form));
        }

        [Fact]
        public void DictsearchOfAnObjectThatIsNoDictionary_GivesNil()
        {
            CadCommandSession session = WithDictionary();
            Value(session, "(setq ln (progn (entmake '((0 . \"LINE\")(10 0.0 0.0 0.0)(11 5.0 5.0 0.0))) (entlast)))");

            // AutoCAD 2021 (d2): a line, a layer's record and an XRECORD are no dictionary: nil, with no error.
            Assert.Equal("nil", Value(session, "(dictsearch ln \"a\")"));
            Assert.Equal("nil", Value(session, "(dictsearch (tblobjname \"LAYER\" \"0\") \"a\")"));
            Assert.Equal("nil", Value(session, "(dictsearch (cdr (assoc -1 (dictsearch d \"aa\"))) \"a\")"));
        }

        #endregion

        #region dictnext

        [Fact]
        public void DictnextWalksTheEntriesByTheirNames_RewindStartingAgain()
        {
            CadCommandSession session = WithDictionary();

            // AutoCAD 2021 (d2): entries added as bb, aa, cc are walked aa, bb, cc; past the last it gives nil and goes on giving
            // nil; with a second argument that is not nil the walk starts again.
            Assert.Equal("\"aa bb cc \"", Names(session, "d"));
            Assert.Equal("nil", Value(session, "(dictnext d)"));
            Assert.Equal("nil", Value(session, "(dictnext d)"));
            Assert.Equal("\"aa\"", Value(session, "(cdr (assoc 1 (dictnext d T)))"));
            Assert.Equal("\"bb\"", Value(session, "(cdr (assoc 1 (dictnext d nil)))"));
        }

        [Fact]
        public void DictnextOfADictionaryNeverWalked_GivesItsFirstEntry()
        {
            CadCommandSession session = WithDictionary();

            Assert.Equal("\"aa\"", Value(session, "(cdr (assoc 1 (dictnext d)))"));
        }

        [Fact]
        public void EachDictionaryKeepsItsOwnPlaceInTheWalk()
        {
            CadCommandSession session = WithDictionary();
            Value(session, "(dictnext d T)");
            Value(session, "(dictnext nod T)");

            // AutoCAD 2021 (l2): the walk of one dictionary goes on where it was after the walk of another.
            Assert.Equal("\"bb\"", Value(session, "(cdr (assoc 1 (dictnext d)))"));
            Assert.Equal("\"cc\"", Value(session, "(cdr (assoc 1 (dictnext d)))"));
        }

        [Fact]
        public void DictsearchWithSetnext_MakesDictnextGiveTheEntryAfter_AndWithoutItNothingMoves()
        {
            CadCommandSession session = WithDictionary();

            // AutoCAD 2021 (d1, l2): (dictsearch d "aa" T) then (dictnext d) is bb; (dictsearch d "cc") without the third argument
            // leaves the place, so the next is cc; a name not found leaves it too.
            Value(session, "(dictsearch d \"aa\" T)");
            Assert.Equal("\"bb\"", Value(session, "(cdr (assoc 1 (dictnext d)))"));
            Value(session, "(dictsearch d \"aa\")");
            Value(session, "(dictsearch d \"nope\" T)");
            Assert.Equal("\"cc\"", Value(session, "(cdr (assoc 1 (dictnext d)))"));
        }

        [Theory]
        [InlineData("(dictnext)", "; error: too few arguments")]
        [InlineData("(dictnext 5)", "; error: bad argument type: lentityp 5")]
        [InlineData("(dictnext nil)", "; error: bad argument type: lentityp nil")]
        [InlineData("(dictnext (namedobjdict) T T)", "; error: too many arguments")]
        public void DictnextWithTheWrongArguments_GivesAutoCadsErrors(string form, string error)
        {
            Assert.Equal(error, Value(Session(Blank()), form));
        }

        #endregion

        #region entmakex

        [Fact]
        public void EntmakexOfAnXrecord_GivesItsNameAndAnEntgetListsItWithNoOwner()
        {
            CadCommandSession session = Session(Blank());

            // AutoCAD 2021 (d1): ((-1 . E) (0 . "XRECORD") (330 . <Entity name: 0>) (5 . "155") (100 . "AcDbXrecord") (280 . 1)
            // (1 . "hello") (10 1.0 2.0 3.0) (70 . 5) (40 . 2.5)): the owner is the name 0 and the flag 280 stands ahead of the data.
            Value(
                session,
                "(setq x (entmakex '((0 . \"XRECORD\")(100 . \"AcDbXrecord\")(1 . \"hello\")(10 1.0 2.0 3.0)(70 . 5)(40 . 2.5))))"
            );

            Assert.Equal("ENAME", Value(session, "(type x)"));
            Assert.Equal(
                "((-1 . <Entity name: *>) (0 . \"XRECORD\") (330 . <Entity name: *>) (5 . \"*\") (100 . \"AcDbXrecord\") (280 . 1) (1 . \"hello\") (10 1.0 2.0 3.0) (70 . 5) (40 . 2.5))",
                Value(session, "(entget x)")
            );
            Assert.Equal(
                "T",
                Value(session, "(if (wcmatch (vl-princ-to-string (cdr (assoc 330 (entget x)))) \"*: 0>\") T)")
            );
        }

        [Fact]
        public void EntmakexWithTheFlagFirst_KeepsThatFlagAndNoOther()
        {
            CadCommandSession session = Session(Blank());

            // AutoCAD 2021 (d1, d3): (280 . 1) first among the data is the flag, not a second one; (280 . 5) (281 . 5) are kept as
            // given; after other data a (280 . 0) is data and the flag (280 . 1) is still ahead.
            Value(session, "(setq x (entmakex '((0 . \"XRECORD\")(100 . \"AcDbXrecord\")(280 . 1)(1 . \"hello\"))))");
            Assert.EndsWith("(100 . \"AcDbXrecord\") (280 . 1) (1 . \"hello\"))", Value(session, "(entget x)"));
            Value(session, "(setq y (entmakex '((0 . \"XRECORD\")(100 . \"AcDbXrecord\")(280 . 5)(281 . 5))))");
            Assert.EndsWith("(100 . \"AcDbXrecord\") (280 . 5) (281 . 5))", Value(session, "(entget y)"));
            Value(session, "(setq z (entmakex '((0 . \"XRECORD\")(100 . \"AcDbXrecord\")(1 . \"a\")(280 . 0))))");
            Assert.EndsWith("(100 . \"AcDbXrecord\") (280 . 1) (1 . \"a\") (280 . 0))", Value(session, "(entget z)"));
        }

        [Fact]
        public void EntmakexOfAnXrecord_TakesEveryKindOfData_AsAutoCadKeepsIt()
        {
            CadCommandSession session = Session(Blank());

            // AutoCAD 2021 (d3): a real for an integer 2 is 2.0, a point of two coordinates has three, 71 is a short whole number
            // (100000 is -31072), 102 groups are kept, and a handle of group 5 after the marker is data.
            Value(
                session,
                "(setq x (entmakex '((0 . \"XRECORD\")(100 . \"AcDbXrecord\")(1 . \"str\")(10 1.0 2.0)(41 . 2)(71 . 100000)(90 . 7)(102 . \"{X\")(300 . \"t\")(310 . \"0A0B\")(5 . \"FF\"))))"
            );

            Assert.EndsWith(
                "(100 . \"AcDbXrecord\") (280 . 1) (1 . \"str\") (10 1.0 2.0 0.0) (41 . 2.0) (71 . -31072) (90 . 7) (102 . \"{X\") (300 . \"t\") (310 . \"0A0B\") (5 . \"*\"))",
                Value(session, "(entget x)")
            );
        }

        [Theory]
        [InlineData("'((0 . \"XRECORD\"))")]
        [InlineData("'((0 . \"XRECORD\")(1 . \"a\"))")]
        [InlineData("'((0 . \"DICTIONARY\"))")]
        [InlineData("'((0 . \"DICTIONARY\")(3 . \"a\"))")]
        [InlineData("'((0 . \"XRECORD\")(100 . \"AcDbXrecord\")(1 . \"a\")(100 . \"Other\")(1 . \"b\"))")]
        [InlineData("'((0 . \"XRECORD\")(100 . \"AcDbXrecord\")(0 . \"LINE\"))")]
        [InlineData("'((0 . \"XRECORD\")(100 . \"AcDbXrecord\")(999 . \"c\"))")]
        [InlineData("'((0 . \"LINE\")(10 0.0 0.0 0.0))")]
        [InlineData("'((0 . \"NOPE\")(10 0.0 0.0 0.0))")]
        [InlineData("nil")]
        public void EntmakexOfAListThatMakesNothing_GivesNil(string list)
        {
            // AutoCAD 2021 (d2, d3): without its subclass marker an XRECORD or a DICTIONARY is nil, as are a second marker, a 0 and
            // a 999 in the data, a line short of its end and a type that is none.
            Assert.Equal("nil", Value(Session(Blank()), "(entmakex " + list + ")"));
        }

        [Theory]
        [InlineData("(1071 . 5)", "(1071 . 5)")]
        [InlineData("(1000 . \"x\")", "(1000 . \"x\")")]
        [InlineData("(1010 1.0 2.0 3.0)", "(1010 1.0 2.0 3.0)")]
        [InlineData("(-1 . 5)", "(-1 . 5)")]
        [InlineData("(2000 . 5)", "(2000 . 5)")]
        [InlineData("(70 . \"x\")", "(70 . \"x\")")]
        [InlineData("(40 . \"x\")", "(40 . \"x\")")]
        [InlineData("(1 . 5)", "(1 . 5)")]
        [InlineData("(70 . 1.5)", "(70 . 1.5)")]
        [InlineData("(10 . 5)", "(10 . 5)")]
        public void EntmakexOfAnXrecordWithAGroupOfTheWrongKind_IsBadDxfGroup(string group, string shown)
        {
            // AutoCAD 2021 (d2, d3): a code of 1000 or more or below 0, or a value of another kind than the code holds.
            Assert.Equal(
                "; error: bad DXF group: " + shown,
                Value(Session(Blank()), "(entmakex '((0 . \"XRECORD\")(100 . \"AcDbXrecord\")" + group + "))")
            );
        }

        [Fact]
        public void EntmakexOfAnXrecordWithAnOwnerThatIsNoName_IsBadDxfGroup()
        {
            CadCommandSession session = Session(Blank());

            // AutoCAD 2021 (d3): (330 . 0) before the marker is bad; the name of the root dictionary is accepted and not kept.
            Assert.Equal(
                "; error: bad DXF group: (330 . 0)",
                Value(session, "(entmakex '((0 . \"XRECORD\")(330 . 0)(100 . \"AcDbXrecord\")(1 . \"a\")))")
            );
            Value(
                session,
                "(setq x (entmakex (list '(0 . \"XRECORD\") (cons 330 (namedobjdict)) '(100 . \"AcDbXrecord\") '(1 . \"a\"))))"
            );
            Assert.Equal(
                "T",
                Value(session, "(if (wcmatch (vl-princ-to-string (cdr (assoc 330 (entget x)))) \"*: 0>\") T)")
            );
        }

        [Fact]
        public void EntmakexWithABadListAndTooManyArguments_GivesAutoCadsErrors()
        {
            CadCommandSession session = Session(Blank());

            Assert.Equal("; error: bad list in entity tail: 5", Value(session, "(entmakex 5)"));
            Assert.Equal(
                "; error: too many arguments",
                Value(session, "(entmakex '((0 . \"LINE\")(10 0.0 0.0 0.0)(11 5.0 5.0 0.0)) 1)")
            );
            Assert.Equal("nil", Value(session, "(entmakex)"));
        }

        [Fact]
        public void EntmakeOfAnXrecordAndOfADictionary_GiveTheListAndLeaveEntlastAlone()
        {
            CadCommandSession session = Session(Blank());
            Value(session, "(setq l0 (progn (entmake '((0 . \"LINE\")(10 0.0 0.0 0.0)(11 5.0 5.0 0.0))) (entlast)))");

            // AutoCAD 2021 (d3): entmake gives the list it was given, and (entlast) is the line still.
            Assert.Equal(
                "((0 . \"XRECORD\") (100 . \"AcDbXrecord\") (1 . \"viamake\"))",
                Value(session, "(entmake '((0 . \"XRECORD\")(100 . \"AcDbXrecord\")(1 . \"viamake\")))")
            );
            Assert.Equal(
                "((0 . \"DICTIONARY\") (100 . \"AcDbDictionary\"))",
                Value(session, "(entmake '((0 . \"DICTIONARY\")(100 . \"AcDbDictionary\")))")
            );
            Assert.Equal("T", Value(session, "(eq (entlast) l0)"));
        }

        [Fact]
        public void EntmakexOfALine_GivesTheNameOfTheEntityAndOfALayer_TheNameOfItsRecord()
        {
            CadCommandSession session = Session(Blank());

            // AutoCAD 2021 (d2): (entmakex '((0 . "LINE") ...)) is the name of the line, which entlast gives too; a LAYER record
            // gives the name of the record; a line short of its end is nil.
            Value(session, "(setq x (entmakex '((0 . \"LINE\")(10 0.0 0.0 0.0)(11 5.0 5.0 0.0))))");
            Assert.Equal("T", Value(session, "(eq x (entlast))"));
            Assert.Equal("\"LINE\"", Value(session, "(cdr (assoc 0 (entget x)))"));
            Value(
                session,
                "(setq r (entmakex '((0 . \"LAYER\")(100 . \"AcDbSymbolTableRecord\")(100 . \"AcDbLayerTableRecord\")(2 . \"GHXL\")(70 . 0)(62 . 3)(6 . \"Continuous\"))))"
            );
            Assert.Equal("T", Value(session, "(eq r (tblobjname \"LAYER\" \"GHXL\"))"));
            Assert.Equal("nil", Value(session, "(entmakex '((0 . \"LINE\")(10 0.0 0.0 0.0)))"));
        }

        [Fact]
        public void EntmakexOfADictionaryWithEntries_ListsThemByNameAndTheObjectsAreItsOwn()
        {
            CadCommandSession session = Session(Blank());
            Value(session, "(setq pre (entmakex '((0 . \"XRECORD\")(100 . \"AcDbXrecord\")(1 . \"pre\"))))");
            Value(session, "(setq q (entmakex '((0 . \"XRECORD\")(100 . \"AcDbXrecord\")(1 . \"q\"))))");

            // AutoCAD 2021 (d3): a DICTIONARY made with (3 . "k") (350 . pre) lists the entry and takes the object; the entries are
            // listed by name whatever the order they were given in; a name that is no ename is bad.
            Value(
                session,
                "(setq d (entmakex (list '(0 . \"DICTIONARY\") '(100 . \"AcDbDictionary\") (cons 3 \"k\") (cons 350 pre) (cons 3 \"a\") (cons 350 q))))"
            );

            Assert.Equal(
                "((-1 . <Entity name: *>) (0 . \"DICTIONARY\") (330 . <Entity name: *>) (5 . \"*\") (100 . \"AcDbDictionary\") (280 . 0) (281 . 1) (3 . \"a\") (350 . <Entity name: *>) (3 . \"k\") (350 . <Entity name: *>))",
                Value(session, "(entget d)")
            );
            Assert.Equal("T", Value(session, "(equal (cdr (assoc 330 (entget pre))) d)"));
            Assert.Equal(
                "; error: bad DXF group: (350 . 5)",
                Value(session, "(entmakex '((0 . \"DICTIONARY\")(100 . \"AcDbDictionary\")(3 . \"k\")(350 . 5)))")
            );
            Assert.Equal(
                "; error: bad DXF group: (350 . \"x\")",
                Value(session, "(entmakex '((0 . \"DICTIONARY\")(100 . \"AcDbDictionary\")(3 . \"k\")(350 . \"x\")))")
            );
        }

        [Fact]
        public void EntmakexOfADictionaryWithItsFlags_KeepsThemAndNamesItsEntriesBy360WhereItOwnsThemHard()
        {
            CadCommandSession session = Session(Blank());
            Value(session, "(setq nod (namedobjdict))");

            // AutoCAD 2021 (d2): a DICTIONARY made with (280 . 1) (281 . 3) lists them as given; a hard owner lists an entry by 360.
            Value(session, "(setq d2 (entmakex '((0 . \"DICTIONARY\")(100 . \"AcDbDictionary\")(280 . 1)(281 . 3))))");
            Assert.EndsWith("(100 . \"AcDbDictionary\") (280 . 1) (281 . 3))", Value(session, "(entget d2)"));
            Value(session, "(dictadd nod \"GHH\" d2)");
            Value(session, "(dictadd d2 \"one\" (entmakex '((0 . \"XRECORD\")(100 . \"AcDbXrecord\")(1 . \"y1\"))))");

            Assert.EndsWith("(3 . \"one\") (360 . <Entity name: *>))", Value(session, "(entget d2)"));
        }

        #endregion

        #region dictadd, dictremove, dictrename

        [Fact]
        public void DictaddOfAnXrecord_GivesTheNameOfTheObjectAndTheObjectHasTheDictionaryAsOwnerAndReactor()
        {
            CadCommandSession session = Session(Blank());
            Value(session, "(setq nod (namedobjdict))");
            Value(session, "(setq x (entmakex '((0 . \"XRECORD\")(100 . \"AcDbXrecord\")(1 . \"hello\"))))");

            // AutoCAD 2021 (d1): (dictadd nod "GHX" x) is x; (dictsearch nod "GHX") then lists the object with the dictionary as
            // reactor and as owner: ((-1 . E) (0 . "XRECORD") (5 . "155") (102 . "{ACAD_REACTORS") (330 . nod) (102 . "}")
            // (330 . nod) (100 . "AcDbXrecord") (280 . 1) (1 . "hello")).
            Assert.Equal("T", Value(session, "(eq x (dictadd nod \"GHX\" x))"));
            Assert.Equal(
                "((-1 . <Entity name: *>) (0 . \"XRECORD\") (5 . \"*\") (102 . \"{ACAD_REACTORS\") (330 . <Entity name: *>) (102 . \"}\") (330 . <Entity name: *>) (100 . \"AcDbXrecord\") (280 . 1) (1 . \"hello\"))",
                Value(session, "(dictsearch nod \"GHX\")")
            );
            Assert.Equal("T", Value(session, "(equal (cdr (assoc 330 (entget x))) nod)"));
        }

        [Fact]
        public void DictaddOfANameTheDictionaryHas_GivesNil_LettersOfEitherCaseTheSame()
        {
            CadCommandSession session = WithDictionary();
            Value(session, "(setq x (entmakex '((0 . \"XRECORD\")(100 . \"AcDbXrecord\")(1 . \"new\"))))");

            // AutoCAD 2021 (d1): the same name, and the same name in other letters, give nil; the same object under another name
            // is accepted.
            Assert.Equal("nil", Value(session, "(dictadd d \"aa\" x)"));
            Assert.Equal("nil", Value(session, "(dictadd d \"AA\" x)"));
            Assert.Equal("T", Value(session, "(eq x (dictadd d \"zz\" x))"));
            Assert.Equal("T", Value(session, "(eq x (dictadd d \"zz2\" x))"));
        }

        [Theory]
        [InlineData("GH X", true)]
        [InlineData("a$b", true)]
        [InlineData("a-b", true)]
        [InlineData("a_b", true)]
        [InlineData("a.b", true)]
        [InlineData("*anon", true)]
        [InlineData("a'b", true)]
        [InlineData("a!b", true)]
        [InlineData("a~b", true)]
        [InlineData("a{b", true)]
        [InlineData("a[b", true)]
        [InlineData("a&b", true)]
        [InlineData("a%b", true)]
        [InlineData("a@b", true)]
        [InlineData("a#b", true)]
        [InlineData("a^b", true)]
        [InlineData("a+b", true)]
        [InlineData("a(b", true)]
        [InlineData("", false)]
        [InlineData(" lead", false)]
        [InlineData("trail ", false)]
        [InlineData("a<b", false)]
        [InlineData("a>b", false)]
        [InlineData("a;b", false)]
        [InlineData("a,b", false)]
        [InlineData("a/b", false)]
        [InlineData("a|b", false)]
        [InlineData("a=b", false)]
        [InlineData("a`b", false)]
        [InlineData("a*b", false)]
        [InlineData("a?b", false)]
        [InlineData("a:b", false)]
        public void DictaddOfAName_TakesTheNamesAutoCadTakes(string name, bool taken)
        {
            CadCommandSession session = Session(Blank());
            Value(session, "(setq nod (namedobjdict))");
            Value(session, "(setq x (entmakex '((0 . \"XRECORD\")(100 . \"AcDbXrecord\"))))");

            // AutoCAD 2021 (d3): the names with one of < > / \ " : ; ? * | , = ` in them, or a space at either end, or none, give
            // nil; the other punctuation is taken, a * at the beginning too.
            Assert.Equal(taken ? "T" : "nil", Value(session, "(if (dictadd nod \"" + name + "\" x) T)"));
        }

        [Theory]
        [InlineData("a\\\\b")]
        [InlineData("a\\\"b")]
        public void DictaddOfANameWithABackslashOrAQuote_GivesNil(string name)
        {
            CadCommandSession session = Session(Blank());
            Value(session, "(setq x (entmakex '((0 . \"XRECORD\")(100 . \"AcDbXrecord\"))))");

            Assert.Equal("nil", Value(session, "(dictadd (namedobjdict) \"" + name + "\" x)"));
        }

        [Fact]
        public void DictaddOfWhatIsNoObjectOrNoDictionary_GivesNil_AndOfTheWrongArgumentsTheErrorsOfAutoCad()
        {
            CadCommandSession session = WithDictionary();
            Value(session, "(setq ln (progn (entmake '((0 . \"LINE\")(10 0.0 0.0 0.0)(11 5.0 5.0 0.0))) (entlast)))");
            Value(session, "(setq x (entmakex '((0 . \"XRECORD\")(100 . \"AcDbXrecord\"))))");

            // AutoCAD 2021 (d2): an entity as the object, a dictionary that is none, the dictionary itself: nil.
            Assert.Equal("nil", Value(session, "(dictadd nod \"L\" ln)"));
            Assert.Equal("nil", Value(session, "(dictadd ln \"a\" x)"));
            Assert.Equal("nil", Value(session, "(dictadd x \"a\" x)"));
            Assert.Equal("nil", Value(session, "(dictadd (tblobjname \"LAYER\" \"0\") \"a\" x)"));
            Assert.Equal("nil", Value(session, "(dictadd nod \"ND\" nod)"));
            Assert.Equal("nil", Value(session, "(dictadd d \"self\" d)"));
            Assert.Equal("T", Value(session, "(if (dictadd d \"up\" nod) T)"));
            Assert.Equal("; error: bad argument type: lentityp nil", Value(session, "(dictadd nod \"a\" nil)"));
            Assert.Equal("; error: bad argument type: lentityp 5", Value(session, "(dictadd nod \"a\" 5)"));
            Assert.Equal("; error: bad argument type: stringp 5", Value(session, "(dictadd nod 5 x)"));
            Assert.Equal("; error: bad argument type: lentityp 5", Value(session, "(dictadd 5 \"a\" x)"));
            Assert.Equal("; error: too few arguments", Value(session, "(dictadd nod \"a\")"));
            Assert.Equal("; error: too many arguments", Value(session, "(dictadd nod \"a\" x x)"));
        }

        [Fact]
        public void DictaddOfAnObjectWithAnOwner_LeavesItsOwner()
        {
            CadCommandSession session = WithDictionary();
            Value(session, "(setq h (entmakex '((0 . \"DICTIONARY\")(100 . \"AcDbDictionary\")(280 . 1))))");
            Value(session, "(dictadd nod \"GHH\" h)");
            Value(session, "(setq y (entmakex '((0 . \"XRECORD\")(100 . \"AcDbXrecord\")(1 . \"y\"))))");
            Value(session, "(dictadd h \"one\" y)");

            // AutoCAD 2021 (d2): y owned by h, added to the root too, keeps h as its owner.
            Assert.Equal("T", Value(session, "(if (dictadd nod \"GHY\" y) T)"));
            Assert.Equal("T", Value(session, "(equal (cdr (assoc 330 (entget y))) h)"));
        }

        [Fact]
        public void DictremoveOfAName_GivesTheObjectAndKeepsItWithItsOwner()
        {
            CadCommandSession session = WithDictionary();
            Value(session, "(setq x (dictsearch d \"bb\"))");
            Value(session, "(setq xe (cdr (assoc -1 x)))");

            // AutoCAD 2021 (d1): (dictremove nod "GHY2") is the name of the object; the object is not erased, its list has no
            // reactors any more and the dictionary as its owner (the owner before the handle, as a loose object's is listed).
            Assert.Equal("T", Value(session, "(eq xe (dictremove d \"BB\"))"));
            Assert.Equal("nil", Value(session, "(dictsearch d \"bb\")"));
            Assert.Equal("\"aa cc \"", Names(session, "d"));
            Assert.Equal(
                "((-1 . <Entity name: *>) (0 . \"XRECORD\") (330 . <Entity name: *>) (5 . \"*\") (100 . \"AcDbXrecord\") (280 . 1) (1 . \"bb\"))",
                Value(session, "(entget xe)")
            );
            Assert.Equal("T", Value(session, "(equal (cdr (assoc 330 (entget xe))) d)"));
        }

        [Theory]
        [InlineData("(dictremove d \"NOPE\")", "nil")]
        [InlineData("(dictremove d 5)", "; error: bad argument type: stringp 5")]
        [InlineData("(dictremove 5 \"a\")", "; error: bad argument type: lentityp 5")]
        [InlineData("(dictremove d)", "; error: too few arguments")]
        [InlineData("(dictremove d \"aa\" 5)", "; error: too many arguments")]
        public void DictremoveOfWhatIsNotThere_GivesNilOrAutoCadsError(string form, string expected)
        {
            Assert.Equal(expected, Value(WithDictionary(), form));
        }

        [Fact]
        public void DictrenameOfAName_GivesTheNewNameAsTyped_AndTheEntryTakesItsPlaceByIt()
        {
            CadCommandSession session = WithDictionary();

            // AutoCAD 2021 (d1): (dictrename nod "GHX" "GHY") is "GHY"; (dictrename nod "GHY" "ghy2") is "ghy2" and
            // (dictsearch nod "GHY2") finds it.
            Assert.Equal("\"zz\"", Value(session, "(dictrename d \"aa\" \"zz\")"));
            Assert.Equal("\"bb cc aa \"", Names(session, "d"));
            Assert.Equal("\"Mm\"", Value(session, "(dictrename d \"ZZ\" \"Mm\")"));
            Assert.Equal("\"hello\"", Value(session, "(if (dictsearch d \"mm\") \"hello\")"));
        }

        [Theory]
        [InlineData("(dictrename d \"NOPE\" \"zz\")", "nil")]
        [InlineData("(dictrename d \"aa\" \"aa\")", "nil")]
        [InlineData("(dictrename d \"aa\" \"BB\")", "nil")]
        [InlineData("(dictrename d \"aa\" \"\")", "nil")]
        [InlineData("(dictrename d \"aa\" \"a<b\")", "nil")]
        [InlineData("(dictrename d \"aa\" 5)", "; error: bad argument type: stringp 5")]
        [InlineData("(dictrename d 5 \"a\")", "; error: bad argument type: stringp 5")]
        [InlineData("(dictrename 5 \"aa\" \"a\")", "; error: bad argument type: lentityp 5")]
        [InlineData("(dictrename d \"aa\")", "; error: too few arguments")]
        public void DictrenameThatCannotBeDone_GivesNilOrAutoCadsError(string form, string expected)
        {
            Assert.Equal(expected, Value(WithDictionary(), form));
        }

        [Fact]
        public void AnEntryAddedAnywhere_StandsAmongTheOthersOrderedByTheirLowerCaseNames()
        {
            CadCommandSession session = Session(Blank());
            Value(session, "(setq nod (namedobjdict))");

            // AutoCAD 2021 (l1, l2): the entries of a dictionary are ordered by their names in lower case, "10" before "9", "_x"
            // before "Alpha", "B" and "b" the one entry.
            Value(session, "(setq d (entmakex '((0 . \"DICTIONARY\")(100 . \"AcDbDictionary\"))))");
            Value(session, "(dictadd nod \"GHD\" d)");

            foreach (string name in new[] { "kp", "Alpha", "_x", "10", "9", "ke" })
            {
                Value(
                    session,
                    "(dictadd d \""
                        + name
                        + "\" (entmakex '((0 . \"XRECORD\")(100 . \"AcDbXrecord\")(1 . \""
                        + name
                        + "\"))))"
                );
            }

            Assert.Equal("\"10 9 _x Alpha ke kp \"", Names(session, "d"));
        }

        #endregion

        #region entdel

        [Fact]
        public void EntdelOfAnObjectADictionaryHolds_ErasesItAndTakesItsEntryOut()
        {
            CadCommandSession session = WithDictionary();
            Value(session, "(setq x (cdr (assoc -1 (dictsearch d \"bb\"))))");

            // AutoCAD 2021 (d1): (entdel x) is x, (entget x) then nil and the entry is gone.
            Assert.Equal("T", Value(session, "(eq x (entdel x))"));
            Assert.Equal("nil", Value(session, "(entget x)"));
            Assert.Equal("nil", Value(session, "(dictsearch d \"bb\")"));
            Assert.Equal("\"aa cc \"", Names(session, "d"));
        }

        [Fact]
        public void EntdelOfADictionary_TakesItsEntriesAndItsEntryInTheRootWithIt()
        {
            CadCommandSession session = WithDictionary();

            Assert.Equal("T", Value(session, "(eq d (entdel d))"));
            Assert.Equal("nil", Value(session, "(entget d)"));
            Assert.Equal("nil", Value(session, "(dictsearch nod \"GHD\")"));
        }

        [Fact]
        public void EntdelOfAnObjectNoDictionaryHolds_LeavesItAndOfTheRootDictionaryGivesNil()
        {
            CadCommandSession session = Session(Blank());
            Value(session, "(setq x (entmakex '((0 . \"XRECORD\")(100 . \"AcDbXrecord\")(1 . \"loose\"))))");

            // AutoCAD 2021 (d3): (entdel x) of a loose object is x each time and (entget x) still lists it; (entdel nod) is nil.
            Assert.Equal("T", Value(session, "(eq x (entdel x))"));
            Assert.Equal("T", Value(session, "(eq x (entdel x))"));
            Assert.Equal("\"XRECORD\"", Value(session, "(cdr (assoc 0 (entget x)))"));
            Assert.Equal("nil", Value(session, "(entdel (namedobjdict))"));
            Assert.Equal("\"DICTIONARY\"", Value(session, "(cdr (assoc 0 (entget (namedobjdict))))"));
        }

        [Fact]
        public void HandentFindsAnObjectByItsHandle_AndEntupdAndEntnextGiveNilForOne()
        {
            CadCommandSession session = Session(Blank());
            Value(session, "(setq x (entmakex '((0 . \"XRECORD\")(100 . \"AcDbXrecord\")(1 . \"a\"))))");
            Value(session, "(setq h (cdr (assoc 5 (entget x))))");

            // AutoCAD 2021 (d1, d3): (handent h) is the name of the object; entupd and entnext of an object are nil.
            Assert.Equal("T", Value(session, "(eq x (handent h))"));
            Assert.Equal("nil", Value(session, "(entupd x)"));
            Assert.Equal("nil", Value(session, "(entnext x)"));
        }

        #endregion

        #region Save and open

        [Fact]
        public void ADictionaryWithItsXrecords_SurvivesASaveAsDxfAndAnOpen()
        {
            CadCommandSession session = WithDictionary();

            CadCommandSession again = Session(
                CadDocument.From(DxfDatabase.Parse(session.Document.Database.ToText()), "d.dxf")
            );

            Assert.Equal("\"aa bb cc \"", Names(again, "(cdr (assoc -1 (dictsearch (namedobjdict) \"GHD\")))"));
            Assert.Equal(
                "((-1 . <Entity name: *>) (0 . \"XRECORD\") (5 . \"*\") (102 . \"{ACAD_REACTORS\") (330 . <Entity name: *>) (102 . \"}\") (330 . <Entity name: *>) (100 . \"AcDbXrecord\") (280 . 1) (1 . \"bb\"))",
                Value(again, "(dictsearch (cdr (assoc -1 (dictsearch (namedobjdict) \"GHD\"))) \"bb\")")
            );
        }

        [Fact]
        public void ADictionaryWithItsXrecords_SurvivesASaveAsDwgAndAnOpen()
        {
            CadCommandSession session = WithDictionary();
            string path = Path.Combine(Path.GetTempPath(), "dict-" + Guid.NewGuid().ToString("N") + ".dwg");

            try
            {
                DwgWriter.Write(session.Document.Database, path);
                CadCommandSession again = Session(CadDocument.From(DwgReader.Open(path), "d.dwg"));

                Assert.Equal("\"aa bb cc \"", Names(again, "(cdr (assoc -1 (dictsearch (namedobjdict) \"GHD\")))"));
                Assert.Equal(
                    "\"cc\"",
                    Value(
                        again,
                        "(cdr (assoc 1 (dictsearch (cdr (assoc -1 (dictsearch (namedobjdict) \"GHD\"))) \"cc\")))"
                    )
                );
            }
            finally
            {
                File.Delete(path);
            }
        }

        #endregion
    }
}
