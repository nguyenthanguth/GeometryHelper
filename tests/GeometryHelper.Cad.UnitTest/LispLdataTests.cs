using System;
using System.IO;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// <c>vlax-ldata-put</c>, <c>-get</c>, <c>-delete</c>, <c>-list</c> and <c>-test</c>, with the rows AutoCAD 2021's
    /// accoreconsole gave (probes l1 and l2 of the C4b measurements, kept in the transcripts of the lispux scratch). An entity
    /// name and a handle are written <c>*</c>, as the harness normalises them.
    /// </summary>
    public sealed class LispLdataTests : IDisposable
    {
        // Ends a routine a test left waiting, so no worker thread outlives its test.
        public void Dispose() => WaitingRoutines.EndAll();

        private static string Value(CadCommandSession session, string form) => LispEntityTests.Value(session, form);

        private static CadCommandSession Loaded()
        {
            CadCommandSession session = Session(Blank());
            Value(session, "(vl-load-com)");
            Value(session, "(setvar \"CMDECHO\" 0)");
            Value(session, "(setq e (progn (entmake '((0 . \"LINE\")(10 0.0 0.0 0.0)(11 5.0 5.0 0.0))) (entlast)))");
            return session;
        }

        #region Put, get, delete and list

        [Fact]
        public void PutOfAnInteger_GivesTheData_AndGetFindsItUnderTheKeyInEitherCase()
        {
            CadCommandSession session = Loaded();

            // AutoCAD 2021 (l1): (vlax-ldata-put "GHD1" "k1" 5) is 5; (vlax-ldata-get "GHD1" "K1") is 5.
            Assert.Equal("5", Value(session, "(vlax-ldata-put \"GHD1\" \"k1\" 5)"));
            Assert.Equal("5", Value(session, "(vlax-ldata-get \"GHD1\" \"k1\")"));
            Assert.Equal("5", Value(session, "(vlax-ldata-get \"GHD1\" \"K1\")"));
            Assert.Equal("5", Value(session, "(vlax-ldata-get \"ghd1\" \"k1\")"));
        }

        [Fact]
        public void GetOfAKeyOrADictionaryThatIsNotThere_GivesTheDefaultOrNil()
        {
            CadCommandSession session = Loaded();
            Value(session, "(vlax-ldata-put \"GHD1\" \"k1\" 5)");

            // AutoCAD 2021 (l1): the third argument is what a missing key gives, in a dictionary there or not; data that is nil
            // is nil, not the default.
            Assert.Equal("nil", Value(session, "(vlax-ldata-get \"GHD1\" \"nokey\")"));
            Assert.Equal("\"dflt\"", Value(session, "(vlax-ldata-get \"GHD1\" \"nokey\" \"dflt\")"));
            Assert.Equal("\"dflt\"", Value(session, "(vlax-ldata-get \"NODICT\" \"nokey\" \"dflt\")"));
            Assert.Equal("nil", Value(session, "(vlax-ldata-get \"NODICT\" \"nokey\")"));
            Value(session, "(vlax-ldata-put \"GHD1\" \"k6\" nil)");
            Assert.Equal("nil", Value(session, "(vlax-ldata-get \"GHD1\" \"k6\" \"dflt\")"));
        }

        [Theory]
        [InlineData("\"text\"", "\"text\"")]
        [InlineData("1.5", "1.5")]
        [InlineData("-0.5", "-0.5")]
        [InlineData("'(1 2 \"abc\" (3 4))", "(1 2 \"abc\" (3 4))")]
        [InlineData("'(1 . 2)", "(1 . 2)")]
        [InlineData("nil", "nil")]
        [InlineData("'SYM", "SYM")]
        [InlineData("T", "T")]
        [InlineData("'(1.0 2.0 3.0)", "(1.0 2.0 3.0)")]
        [InlineData("'(1.5 2)", "(1.5 2)")]
        [InlineData("(list 100000 -5 3.0e10 \"x\" 'a)", "(100000 -5 3.0e+10 \"x\" A)")]
        [InlineData("'((a . 1) (b . \"x\"))", "((A . 1) (B . \"x\"))")]
        [InlineData("-7", "-7")]
        public void PutAndGetOfData_GiveItBackAsItWas(string data, string expected)
        {
            CadCommandSession session = Loaded();

            // AutoCAD 2021 (l1, l2): the data comes back as it was put, symbols in upper case, a real with the digits it had.
            Assert.Equal(expected, Value(session, "(vlax-ldata-put \"GHD1\" \"k\" " + data + ")"));
            Assert.Equal(expected, Value(session, "(vlax-ldata-get \"GHD1\" \"k\")"));
        }

        [Fact]
        public void PutOfALetterBeyondAscii_KeepsTheLetter()
        {
            CadCommandSession session = Loaded();

            Value(session, "(vlax-ldata-put \"GHD1\" \"k\" \"café 中\")");

            Assert.Equal("\"café 中\"", Value(session, "(vlax-ldata-get \"GHD1\" \"k\")"));
        }

        [Fact]
        public void PutOfTheNameOfAnEntity_KeepsTheNameAsAGroup340()
        {
            CadCommandSession session = Loaded();

            // AutoCAD 2021 (l1, l2): the name of the line comes back, the list of the datum holds it as a 340 and the text is
            // (&VLO-E . 0).
            Assert.Equal("T", Value(session, "(eq e (vlax-ldata-put \"GHD1\" \"ke\" e))"));
            Assert.Equal("T", Value(session, "(eq e (vlax-ldata-get \"GHD1\" \"ke\"))"));
            Assert.Equal("(\"ke\" . <Entity name: *>)", Value(session, "(car (vlax-ldata-list \"GHD1\"))"));
            Value(session, "(setq dd (cdr (assoc -1 (dictsearch (namedobjdict) \"GHD1\"))))");
            Assert.Equal("\"(&VLO-E . 0)\"", Value(session, "(cdr (assoc 300 (dictnext dd T)))"));
            Assert.Equal("T", Value(session, "(eq e (cdr (assoc 340 (dictnext dd T))))"));
        }

        [Fact]
        public void TheTextOfADatum_IsWhatPrin1PrintsWithRealsAsGroups40()
        {
            CadCommandSession session = Loaded();
            Value(session, "(vlax-ldata-put \"GHD1\" \"k1\" 6)");
            Value(session, "(vlax-ldata-put \"GHD1\" \"k2\" \"text\")");
            Value(session, "(vlax-ldata-put \"GHD1\" \"k3\" 1.5)");
            Value(session, "(vlax-ldata-put \"GHD1\" \"k4\" '(1.5 2))");
            Value(session, "(vlax-ldata-put \"GHD1\" \"k5\" nil)");
            Value(session, "(setq dd (cdr (assoc -1 (dictsearch (namedobjdict) \"GHD1\"))))");

            // AutoCAD 2021 (l1, l2): 6 is (91 . 1) (92 . 0) (300 . "6"); "text" is (91 . 6) (92 . 0) (300 . "\"text\""); 1.5 is
            // (91 . 12) (92 . 1) (40 . 1.5) (300 . "(&VLO-R . 0)"); (1.5 2) is (91 . 16) (92 . 1) (40 . 1.5)
            // (300 . "((&VLO-R . 0) 2)"); nil has (91 . 0) and no 92 and no 300. The groups of the object follow the marker
            // (90 . -64512) (91 ...) (92 ...) (40 ...) (300 ...), as the presenter keeps them in an XRECORD.
            Assert.Equal("((90 . -64512) (91 . 1) (92 . 0) (300 . \"6\"))", Data(session, "(dictnext dd T)"));
            Assert.Equal("((90 . -64512) (91 . 6) (92 . 0) (300 . \"\\\"text\\\"\"))", Data(session, "(dictnext dd)"));
            Assert.Equal(
                "((90 . -64512) (91 . 12) (92 . 1) (40 . 1.5) (300 . \"(&VLO-R . 0)\"))",
                Data(session, "(dictnext dd)")
            );
            Assert.Equal(
                "((90 . -64512) (91 . 16) (92 . 1) (40 . 1.5) (300 . \"((&VLO-R . 0) 2)\"))",
                Data(session, "(dictnext dd)")
            );
            Assert.Equal("((90 . -64512) (91 . 0))", Data(session, "(dictnext dd)"));
        }

        // The groups of a datum after the subclass marker and the flag.
        private static string Data(CadCommandSession session, string form) =>
            Value(
                session,
                "(cdr (member '(100 . \"AcDbXrecord\") (vl-remove-if '(lambda (g) (equal g '(280 . 1))) " + form + ")))"
            );

        [Fact]
        public void PutUnderAKeyThatIsThere_ReplacesTheDatum_AndTheKeyTakesTheLettersOfTheNewOne()
        {
            CadCommandSession session = Loaded();

            // AutoCAD 2021 (l2): "b" put as 1 and "B" put as 2 are the one entry, named B, of 2.
            Value(session, "(vlax-ldata-put \"GHD1\" \"b\" 1)");
            Value(session, "(vlax-ldata-put \"GHD1\" \"B\" 2)");

            Assert.Equal("((\"B\" . 2))", Value(session, "(vlax-ldata-list \"GHD1\")"));
        }

        [Fact]
        public void ListGivesTheDataLastFirstAsPairsOfTheKeyAndTheData()
        {
            CadCommandSession session = Loaded();

            // AutoCAD 2021 (l1, l2): the keys 10, 9, _x, Alpha are listed Alpha, _x, 9, 10: the entries of the dictionary are by
            // name and the list is from the end; a datum that is a list is (key 1 2), nil is (key).
            foreach (string key in new[] { "Alpha", "_x", "10", "9" })
            {
                Value(session, "(vlax-ldata-put \"GHD1\" \"" + key + "\" 1)");
            }

            Value(session, "(vlax-ldata-put \"GHD1\" \"k9\" '(1.0 2.0 3.0))");
            Value(session, "(vlax-ldata-put \"GHD1\" \"k6\" nil)");

            Assert.Equal(
                "((\"k9\" 1.0 2.0 3.0) (\"k6\") (\"Alpha\" . 1) (\"_x\" . 1) (\"9\" . 1) (\"10\" . 1))",
                Value(session, "(vlax-ldata-list \"GHD1\")")
            );
            Assert.Equal("nil", Value(session, "(vlax-ldata-list \"NODICT\")"));
        }

        [Fact]
        public void DeleteOfAKey_GivesTAndTakesTheDatumOut_AndNilWhereThereIsNone()
        {
            CadCommandSession session = Loaded();
            Value(session, "(vlax-ldata-put \"GHD1\" \"kp\" \"priv\" T)");
            Value(session, "(vlax-ldata-put \"GHD1\" \"k1\" 1)");

            // AutoCAD 2021 (l1): the fourth argument, private, is read and changes nothing; delete of a key is T, of it again nil,
            // of a key never there nil, in a dictionary never there nil.
            Assert.Equal("\"priv\"", Value(session, "(vlax-ldata-get \"GHD1\" \"kp\")"));
            Assert.Equal("T", Value(session, "(vlax-ldata-delete \"GHD1\" \"kp\")"));
            Assert.Equal("nil", Value(session, "(vlax-ldata-delete \"GHD1\" \"kp\")"));
            Assert.Equal("nil", Value(session, "(vlax-ldata-delete \"GHD1\" \"nokey\")"));
            Assert.Equal("nil", Value(session, "(vlax-ldata-delete \"NODICT\" \"nokey\")"));
            Assert.Equal("((\"k1\" . 1))", Value(session, "(vlax-ldata-list \"GHD1\")"));
        }

        [Theory]
        [InlineData("")]
        [InlineData("bad<key")]
        public void PutUnderAKeyAnEntryCannotHave_GivesTheDataAndKeepsNone(string key)
        {
            CadCommandSession session = Loaded();
            Value(session, "(vlax-ldata-put \"GHD1\" \"k1\" 6)");

            // AutoCAD 2021 (l1, l2): "" and "bad<key" give the data back and are not in the list afterwards.
            Assert.Equal("1", Value(session, "(vlax-ldata-put \"GHD1\" \"" + key + "\" 1)"));
            Assert.Equal("((\"k1\" . 6))", Value(session, "(vlax-ldata-list \"GHD1\")"));
        }

        [Fact]
        public void AKeyWithASpaceAndASymbolAsAKey_AreKept()
        {
            CadCommandSession session = Loaded();

            // AutoCAD 2021 (l1): "k k" in "G H D" is kept; a symbol is a key by its name.
            Value(session, "(vlax-ldata-put \"G H D\" \"k k\" 1)");
            Value(session, "(vlax-ldata-put \"G H D\" 'sym 2)");

            Assert.Equal("((\"SYM\" . 2) (\"k k\" . 1))", Value(session, "(vlax-ldata-list \"G H D\")"));
            Assert.Equal("2", Value(session, "(vlax-ldata-get \"G H D\" \"sym\")"));
        }

        #endregion

        #region Where the data is kept

        [Fact]
        public void TheDataOfAName_IsKeptInADictionaryOfThatNameInTheRootDictionary()
        {
            CadCommandSession session = Loaded();

            // AutoCAD 2021 (l1): after the first put (dictsearch (namedobjdict) "GHD1") is a DICTIONARY owned soft by the root,
            // (280 . 0) (281 . 1), whose entries are the keys.
            Assert.Equal("nil", Value(session, "(dictsearch (namedobjdict) \"GHD1\")"));
            Value(session, "(vlax-ldata-put \"GHD1\" \"k1\" 5)");
            Value(session, "(vlax-ldata-put \"GHD1\" \"k2\" 6)");

            Assert.Equal(
                "((-1 . <Entity name: *>) (0 . \"DICTIONARY\") (5 . \"*\") (102 . \"{ACAD_REACTORS\") (330 . <Entity name: *>) (102 . \"}\") (330 . <Entity name: *>) (100 . \"AcDbDictionary\") (280 . 0) (281 . 1) (3 . \"k1\") (350 . <Entity name: *>) (3 . \"k2\") (350 . <Entity name: *>))",
                Value(session, "(dictsearch (namedobjdict) \"GHD1\")")
            );
        }

        [Fact]
        public void TheDataOfAnEntity_IsKeptInItsExtensionDictionary_WhichThePutMakes()
        {
            CadCommandSession session = Loaded();

            // AutoCAD 2021 (l1, l2): (entget e) has no {ACAD_XDICTIONARY before the put and has one after it, listed as
            // ((-1 . E) (0 . "LINE") (5 . "161") (102 . "{ACAD_XDICTIONARY") (360 . E) (102 . "}") (330 . E) (100 . "AcDbEntity")
            // ...): the handle, the dictionary, then the owner; the dictionary is hard owned: (280 . 1) (281 . 1) (3 . "ek1")
            // (360 . E), the keys held in it directly.
            Assert.Equal("nil", Value(session, "(assoc 360 (entget e))"));
            Assert.Equal("77", Value(session, "(vlax-ldata-put e \"ek1\" 77)"));
            Assert.Equal("77", Value(session, "(vlax-ldata-get e \"ek1\")"));
            Assert.Equal("((\"ek1\" . 77))", Value(session, "(vlax-ldata-list e)"));
            Assert.Equal(
                "((-1 . <Entity name: *>) (0 . \"LINE\") (5 . \"*\") (102 . \"{ACAD_XDICTIONARY\") (360 . <Entity name: *>) (102 . \"}\") (330 . <Entity name: *>) (100 . \"AcDbEntity\") (67 . 0) (410 . \"Model\") (8 . \"0\") (100 . \"AcDbLine\") (10 0.0 0.0 0.0) (11 5.0 5.0 0.0) (210 0.0 0.0 1.0))",
                Value(session, "(entget e)")
            );
            Assert.Equal(
                "((-1 . <Entity name: *>) (0 . \"DICTIONARY\") (330 . <Entity name: *>) (5 . \"*\") (100 . \"AcDbDictionary\") (280 . 1) (281 . 1) (3 . \"ek1\") (360 . <Entity name: *>))",
                Value(session, "(entget (cdr (assoc 360 (entget e))))")
            );
        }

        [Fact]
        public void TheDataOfAnObject_IsKeptInTheExtensionDictionaryOfThatObjectAlone()
        {
            CadCommandSession session = Loaded();
            Value(session, "(vlax-ldata-put e \"ek1\" 77)");
            Value(session, "(setq xd (cdr (assoc 360 (entget e))))");

            // AutoCAD 2021 (l2): an entity name as the owner, whatever the object (a dictionary, the root, a layer's record): the
            // data of its own extension dictionary. The data of xd is not that of e, and the data of (namedobjdict) is not
            // among the entries of the root.
            Value(session, "(vlax-ldata-put xd \"viaDict\" 5)");
            Value(session, "(vlax-ldata-put (namedobjdict) \"nk1\" 5)");
            Value(session, "(vlax-ldata-put (tblobjname \"LAYER\" \"0\") \"lk\" 9)");

            Assert.Equal("((\"ek1\" . 77))", Value(session, "(vlax-ldata-list e)"));
            Assert.Equal("((\"viaDict\" . 5))", Value(session, "(vlax-ldata-list xd)"));
            Assert.Equal("((\"nk1\" . 5))", Value(session, "(vlax-ldata-list (namedobjdict))"));
            Assert.Equal("nil", Value(session, "(dictsearch (namedobjdict) \"nk1\")"));
            Assert.Equal("9", Value(session, "(vlax-ldata-get (tblobjname \"LAYER\" \"0\") \"lk\")"));
            Assert.Equal("((\"ek1\" . 77))", Value(session, "(vlax-ldata-list (vlax-ename->vla-object e))"));
        }

        [Fact]
        public void TheDataOfTwoPuts_StandsInTheExtensionDictionaryAsTheListGivesIt()
        {
            CadCommandSession session = Loaded();

            Value(session, "(vlax-ldata-put e \"ek2\" \"two\")");
            Value(session, "(vlax-ldata-put e \"ek3\" '(1 2))");
            Value(session, "(vlax-ldata-put e \"ek1\" 77)");
            Value(session, "(vlax-ldata-delete e \"ek1\")");

            Assert.Equal("((\"ek3\" 1 2) (\"ek2\" . \"two\"))", Value(session, "(vlax-ldata-list e)"));
        }

        #endregion

        #region Test

        [Theory]
        [InlineData("5", "T")]
        [InlineData("1.5", "T")]
        [InlineData("\"s\"", "T")]
        [InlineData("'(1 2)", "T")]
        [InlineData("nil", "T")]
        [InlineData("'sym", "T")]
        [InlineData("T", "T")]
        [InlineData("e", "T")]
        [InlineData("(list 1 e)", "T")]
        [InlineData("'(1 . 2)", "T")]
        [InlineData("(ssget \"_X\")", "nil")]
        [InlineData("car", "nil")]
        [InlineData("(lambda (x) x)", "nil")]
        [InlineData("(vlax-3d-point 1 2 3)", "nil")]
        [InlineData("(vlax-make-variant 5)", "nil")]
        [InlineData("(vlax-make-safearray vlax-vbInteger '(0 . 2))", "nil")]
        [InlineData("(list 1 car)", "nil")]
        public void TestOfAValue_IsTWhereItCanBeKept(string value, string expected)
        {
            CadCommandSession session = Loaded();

            // AutoCAD 2021 (l1): T for a whole number, a real, a string, a list, nil, a symbol, T and the name of an entity; nil
            // for a selection set, a function, a variant and a safe array. A second argument is read and has no effect.
            Assert.Equal(expected, Value(session, "(vlax-ldata-test " + value + ")"));
            Assert.Equal(expected, Value(session, "(vlax-ldata-test " + value + " 2)"));
        }

        #endregion

        #region Errors

        [Fact]
        public void PutOfAFunction_IsAnIllegalImportOrExportObject()
        {
            CadCommandSession session = Loaded();

            // AutoCAD 2021 (l1): ; error: Visual LISP: Illegal import/export object #<SUBR @... CAR>
            Assert.StartsWith(
                "; error: Visual LISP: Illegal import/export object #<SUBR @",
                Value(session, "(vlax-ldata-put \"GHD1\" \"kf\" car)")
            );
            Assert.Equal("nil", Value(session, "(vlax-ldata-get \"GHD1\" \"kf\")"));
        }

        [Fact]
        public void PutOfASelectionSet_GivesItAndGetGivesItBackInTheSession()
        {
            CadCommandSession session = Loaded();
            Value(session, "(setq ss (ssget \"_X\"))");

            // AutoCAD 2021 (l1): the set comes back, held for the session and not kept in the drawing.
            Assert.Equal("T", Value(session, "(eq ss (vlax-ldata-put \"GHD1\" \"kss\" ss))"));
            Assert.Equal("T", Value(session, "(eq ss (vlax-ldata-get \"GHD1\" \"kss\"))"));
        }

        [Theory]
        [InlineData("(vlax-ldata-put \"\" \"k\" 1)", "; error: can't create dictionary: \"\"")]
        [InlineData("(vlax-ldata-put \"bad<name\" \"k\" 1)", "; error: can't create dictionary: \"bad<name\"")]
        [InlineData(
            "(vlax-ldata-put 5 \"k\" 1)",
            "; error: bad argument type: must be VLA-OBJECT/lentity or dictionary name: 5"
        )]
        [InlineData(
            "(vlax-ldata-put nil \"k\" 1)",
            "; error: bad argument type: must be VLA-OBJECT/lentity or dictionary name: nil"
        )]
        [InlineData("(vlax-ldata-put \"GHD1\" 5 1)", "; error: bad argument type: (or stringp symbolp): 5")]
        [InlineData("(vlax-ldata-put \"GHD1\" \"k\")", "; error: too few arguments")]
        [InlineData("(vlax-ldata-put \"GHD1\" \"k\" 1 nil nil)", "; error: too many arguments")]
        [InlineData(
            "(vlax-ldata-get 5 \"k\")",
            "; error: bad argument type: must be VLA-OBJECT/lentity or dictionary name: 5"
        )]
        [InlineData("(vlax-ldata-get \"GHD1\" 5)", "; error: bad argument type: (or stringp symbolp): 5")]
        [InlineData("(vlax-ldata-get \"GHD1\")", "; error: too few arguments")]
        [InlineData("(vlax-ldata-get \"GHD1\" \"k\" 1 1 1)", "; error: too many arguments")]
        [InlineData(
            "(vlax-ldata-delete 5 \"k\")",
            "; error: bad argument type: must be VLA-OBJECT/lentity or dictionary name: 5"
        )]
        [InlineData("(vlax-ldata-delete \"GHD1\" 5)", "; error: bad argument type: (or stringp symbolp): 5")]
        [InlineData("(vlax-ldata-delete \"GHD1\")", "; error: too few arguments")]
        [InlineData("(vlax-ldata-delete \"GHD1\" \"k\" 1 1)", "; error: too many arguments")]
        [InlineData(
            "(vlax-ldata-list 5)",
            "; error: bad argument type: must be VLA-OBJECT/lentity or dictionary name: 5"
        )]
        [InlineData("(vlax-ldata-list)", "; error: too few arguments")]
        [InlineData("(vlax-ldata-list \"GHD1\" 1 1)", "; error: too many arguments")]
        [InlineData("(vlax-ldata-test)", "; error: too few arguments")]
        [InlineData("(vlax-ldata-test 1 2 3)", "; error: too many arguments")]
        public void TheFiveFunctionsWithTheWrongArguments_GiveAutoCadsErrors(string form, string error)
        {
            Assert.Equal(error, Value(Loaded(), form));
        }

        [Fact]
        public void TheFunctionsTakeOneArgumentMoreThanTheirDocumentationAsAutoCadDoes()
        {
            CadCommandSession session = Loaded();
            Value(session, "(vlax-ldata-put \"GHD1\" \"k1\" 6)");

            // AutoCAD 2021 (l1, l2): get with four arguments, list with two, delete with three and test with two are read.
            Assert.Equal("6", Value(session, "(vlax-ldata-get \"GHD1\" \"k1\" 1 1)"));
            Assert.Equal("((\"k1\" . 6))", Value(session, "(vlax-ldata-list \"GHD1\" 1)"));
            Assert.Equal("T", Value(session, "(vlax-ldata-delete \"GHD1\" \"k1\" 1)"));
        }

        #endregion

        #region Save and open

        [Fact]
        public void TheData_SurvivesASaveAsDxfAndAnOpen_WhetherOfAnameOrOfAnEntity()
        {
            CadCommandSession session = Loaded();
            Value(session, "(vlax-ldata-put \"GHD1\" \"k1\" '(1 2.5 \"x\"))");
            Value(session, "(vlax-ldata-put \"GHD1\" \"ke\" e)");
            Value(session, "(vlax-ldata-put e \"ek1\" 77)");

            CadCommandSession again = Session(
                CadDocument.From(DxfDatabase.Parse(session.Document.Database.ToText()), "l.dxf")
            );

            Assert.Equal("(1 2.5 \"x\")", Value(again, "(vlax-ldata-get \"GHD1\" \"k1\")"));
            Assert.Equal("T", Value(again, "(eq (entlast) (vlax-ldata-get \"GHD1\" \"ke\"))"));
            Assert.Equal("77", Value(again, "(vlax-ldata-get (entlast) \"ek1\")"));
        }

        [Fact]
        public void TheData_SurvivesASaveAsDwgAndAnOpen_WhetherOfAnameOrOfAnEntity()
        {
            CadCommandSession session = Loaded();
            Value(session, "(vlax-ldata-put \"GHD1\" \"k1\" '(1 2.5 \"x\"))");
            Value(session, "(vlax-ldata-put e \"ek1\" 77)");
            string path = Path.Combine(Path.GetTempPath(), "ldata-" + Guid.NewGuid().ToString("N") + ".dwg");

            try
            {
                DwgWriter.Write(session.Document.Database, path);
                CadCommandSession again = Session(CadDocument.From(DwgReader.Open(path), "l.dwg"));

                Assert.Equal("(1 2.5 \"x\")", Value(again, "(vlax-ldata-get \"GHD1\" \"k1\")"));
                Assert.Equal("77", Value(again, "(vlax-ldata-get (entlast) \"ek1\")"));
            }
            finally
            {
                File.Delete(path);
            }
        }

        #endregion
    }
}
