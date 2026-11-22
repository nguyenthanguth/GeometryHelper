using System;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// <c>entmod</c> of the records of the tables, of an XRECORD, of a VERTEX of a polyline, and <c>entdel</c> of an entity that
    /// has an extension dictionary, with what AutoCAD 2021's accoreconsole gave for each (the L8 measurements m/entmod, e1 to e5
    /// and d1). A change is made as a routine makes it: the list <c>entget</c> gives, with a group added after it (the last of
    /// two groups of a code wins) or one <c>subst</c>ituted. A record taken is given back as the list; one AutoCAD refuses is
    /// nil and leaves the drawing as it was. The layer list is told (the Changed event has <c>TablesChanged</c>) and one U takes
    /// a change back.
    /// </summary>
    public sealed class LispEntmodRecordTests : IDisposable
    {
        public void Dispose() => WaitingRoutines.EndAll();

        private static string V(CadCommandSession session, string form) => LispEntityTests.Value(session, form);

        private static CadCommandSession WithLayers()
        {
            CadDocument document = Blank();
            document.Database.LayerTable.Add(new DxfLayerTableRecord { Name = "GHA", ColorIndex = 1 });
            document.Database.LayerTable.Add(new DxfLayerTableRecord { Name = "GHB", ColorIndex = 2 });
            return Session(document);
        }

        // The entmod of a record's list given with one group added after it, the way a routine appends one.
        private static string Mod(CadCommandSession session, string table, string name, string group)
        {
            V(session, "(setq e (tblobjname \"" + table + "\" \"" + name + "\"))");
            return V(session, "(entmod (append (entget e) (list " + group + ")))");
        }

        private static string ModLayer(CadCommandSession session, string group, string name = "GHA") =>
            Mod(session, "LAYER", name, group);

        #region Layers

        [Fact]
        public void ALayerGivenAnotherColour_HasItAndTheLayerListIsTold()
        {
            CadCommandSession session = WithLayers();
            bool told = false;
            session.Document.Changed += (s, e) => told |= e.TablesChanged;

            string answer = ModLayer(session, "(cons 62 3)");

            Assert.NotEqual("nil", answer);
            Assert.Equal(3, session.Document.Database.LayerTable["GHA"].ColorIndex);
            Assert.Equal("3", V(session, "(cdr (assoc 62 (tblsearch \"LAYER\" \"GHA\")))"));
            Assert.True(told);
        }

        [Fact]
        public void ALayerGivenANegativeColour_IsOffInThatColour()
        {
            CadCommandSession session = WithLayers();

            ModLayer(session, "(cons 62 -5)");

            Assert.True(session.Document.Database.LayerTable["GHA"].IsOff);
            Assert.Equal(5, session.Document.Database.LayerTable["GHA"].ColorIndex);
            Assert.Equal("-5", V(session, "(cdr (assoc 62 (tblsearch \"LAYER\" \"GHA\")))"));
        }

        [Fact]
        public void TheFrozenBitOfALayersFlags_FreezesItAndTheLockedBitLocksIt_TheCurrentLayerToo()
        {
            CadCommandSession session = WithLayers();
            Run(session, "-LAYER", "Set", "GHA", Enter);

            ModLayer(session, "(cons 70 5)");

            Assert.True(session.Document.Database.LayerTable["GHA"].IsFrozen);
            Assert.True(session.Document.Database.LayerTable["GHA"].IsLocked);
            Assert.Equal("\"GHA\"", V(session, "(getvar \"CLAYER\")"));

            ModLayer(session, "(cons 70 0)");

            Assert.False(session.Document.Database.LayerTable["GHA"].IsFrozen);
            Assert.False(session.Document.Database.LayerTable["GHA"].IsLocked);
        }

        [Fact]
        public void ALayerRenamed_TakesItsObjectsWithIt()
        {
            CadCommandSession session = WithLayers();
            Run(session, "-LAYER", "Set", "GHA", Enter, "LINE", "0,0", "10,0", Enter);

            string answer = ModLayer(session, "(cons 2 \"GHZ\")");

            Assert.NotEqual("nil", answer);
            Assert.True(session.Document.Database.LayerTable.Has("GHZ"));
            Assert.False(session.Document.Database.LayerTable.Has("GHA"));
            Assert.Equal("\"GHZ\"", V(session, "(cdr (assoc 8 (entget (entlast))))"));
            Assert.Equal("\"GHZ\"", V(session, "(getvar \"CLAYER\")"));
        }

        [Theory]
        [InlineData("(cons 2 \"a b\")")]
        [InlineData("(cons 2 \"gha\")")]
        public void ALayerRenamedToASpaceOrToOtherLettersOfItself_IsRenamed(string group)
        {
            CadCommandSession session = WithLayers();

            Assert.NotEqual("nil", ModLayer(session, group));
        }

        [Fact]
        public void DefpointsAndLayerZeroGivenAnotherColour_AreChanged_AndDefpointsRenamed()
        {
            CadCommandSession session = WithLayers();
            Run(session, "-LAYER", "Make", "Defpoints", Enter, Enter);

            Assert.NotEqual("nil", ModLayer(session, "(cons 62 4)", "0"));
            Assert.Equal(4, session.Document.Database.LayerTable["0"].ColorIndex);
            Assert.NotEqual("nil", ModLayer(session, "(cons 2 \"DP2\")", "Defpoints"));
            Assert.True(session.Document.Database.LayerTable.Has("DP2"));
        }

        [Theory]
        [InlineData("(cons 2 \"ZERO\")", "0")]
        [InlineData("(cons 2 \"GHB\")", "GHA")]
        [InlineData("(cons 2 \"A<B\")", "GHA")]
        [InlineData("(cons 2 \"\")", "GHA")]
        [InlineData("(cons 62 300)", "GHA")]
        [InlineData("(cons 62 \"red\")", "GHA")]
        public void AChangeToALayerAutoCadRefuses_GivesNilAndLeavesTheDrawingAsItWas(string group, string layer)
        {
            CadCommandSession session = WithLayers();
            string before = session.Document.Database.ToText();

            Assert.Equal("nil", ModLayer(session, group, layer));
            Assert.Equal(before, session.Document.Database.ToText());
        }

        [Fact]
        public void ALayerGivenALinetypeThatIsNotLoaded_IsNilAndTheLineSaysWhich()
        {
            CadCommandSession session = WithLayers();

            ModLayer(session, "(cons 6 \"HIDDEN\")");

            Assert.Contains(session.History, line =>
                line.Contains("Error: Undefined line type HIDDEN in LayerTableRecord GHA")
            );
        }

        [Fact]
        public void ALayerGivenALineweightThatIsNone_KeepsItsOwnAndTheListIsTaken()
        {
            CadCommandSession session = WithLayers();
            ModLayer(session, "(cons 370 50)");

            Assert.NotEqual("nil", ModLayer(session, "(cons 370 55)"));
            Assert.Equal(DxfLineWeight.LineWeight050, session.Document.Database.LayerTable["GHA"].LineWeight);
        }

        [Fact]
        public void ALayersWholeListGivenBack_IsTakenAsItIs_AndAShortListIsNil()
        {
            CadCommandSession session = WithLayers();
            V(session, "(setq e (tblobjname \"LAYER\" \"GHA\"))");

            Assert.NotEqual("nil", V(session, "(entmod (entget e))"));
            Assert.Equal("nil", V(session, "(entmod (list (cons -1 e) '(0 . \"LAYER\") '(2 . \"GHQ\")))"));
            Assert.False(session.Document.Database.LayerTable.Has("GHQ"));
            Assert.True(session.Document.Database.LayerTable.Has("GHA"));
        }

        [Fact]
        public void OneUndo_TakesBackAnEntmodOfALayer()
        {
            CadCommandSession session = WithLayers();
            Run(session, "REGEN");

            ModLayer(session, "(cons 62 3)");
            Assert.Equal(3, session.Document.Database.LayerTable["GHA"].ColorIndex);

            Run(session, "U");

            Assert.Equal(1, session.Document.Database.LayerTable["GHA"].ColorIndex);
        }

        [Fact]
        public void TwoEntmodsOfLayersTypedOneAfterTheOther_AreOneStepOfUndo()
        {
            CadCommandSession session = WithLayers();
            Run(session, "REGEN");

            ModLayer(session, "(cons 62 3)");
            ModLayer(session, "(cons 62 4)", "GHB");
            Run(session, "U");

            Assert.Equal(1, session.Document.Database.LayerTable["GHA"].ColorIndex);
            Assert.Equal(2, session.Document.Database.LayerTable["GHB"].ColorIndex);
        }

        #endregion

        #region Text styles

        private static string ModStyle(CadCommandSession session, string group, string name = "Standard") =>
            Mod(session, "STYLE", name, group);

        [Fact]
        public void ATextStyleGivenAWidthFactor_HasItAndOneUndoTakesItBack()
        {
            CadCommandSession session = Session(Blank());
            Run(session, "REGEN");

            string answer = ModStyle(session, "(cons 41 0.8)");

            Assert.NotEqual("nil", answer);
            Assert.Equal(0.8, session.Document.Database.TextStyleTable["Standard"].XScale, 9);
            Assert.Equal("0.8", V(session, "(cdr (assoc 41 (tblsearch \"STYLE\" \"Standard\")))"));

            Run(session, "U");

            Assert.Equal(1.0, session.Document.Database.TextStyleTable["Standard"].XScale, 9);
        }

        [Fact]
        public void ATextStyleGivenASlantInRadians_HasItAsTheListOfAutoCadGivesIt_AndItsWholeListIsTakenBack()
        {
            CadCommandSession session = Session(Blank());

            ModStyle(session, "(cons 50 0.261799)");

            Assert.Equal("0.261799", V(session, "(cdr (assoc 50 (tblsearch \"STYLE\" \"Standard\")))"));
            Assert.NotEqual("nil", V(session, "(entmod (entget (tblobjname \"STYLE\" \"Standard\")))"));
            Assert.Equal("0.261799", V(session, "(cdr (assoc 50 (tblsearch \"STYLE\" \"Standard\")))"));
        }

        [Fact]
        public void ATextStyleGivenAFontBigFontAndFixedHeight_HasThem()
        {
            CadCommandSession session = Session(Blank());

            ModStyle(session, "(cons 3 \"simplex\")");
            ModStyle(session, "(cons 4 \"bigfont\")");
            ModStyle(session, "(cons 40 3.5)");

            Assert.Equal("\"simplex\"", V(session, "(cdr (assoc 3 (tblsearch \"STYLE\" \"Standard\")))"));
            Assert.Equal("\"bigfont\"", V(session, "(cdr (assoc 4 (tblsearch \"STYLE\" \"Standard\")))"));
            Assert.Equal("3.5", V(session, "(cdr (assoc 40 (tblsearch \"STYLE\" \"Standard\")))"));
        }

        [Theory]
        [InlineData("(cons 70 1)")]
        [InlineData("(cons 41 \"wide\")")]
        [InlineData("(cons 41 -1.0)")]
        [InlineData("(cons 40 -2.5)")]
        public void AValueOfATextStyleAutoCadRefusesOrAStyleCannotHold_GivesNilAndLeavesTheDrawingAsItWas(string group)
        {
            CadCommandSession session = Session(Blank());
            string before = session.Document.Database.ToText();

            Assert.Equal("nil", ModStyle(session, group));
            Assert.Equal(before, session.Document.Database.ToText());
        }

        [Fact]
        public void ATextStyleOtherThanStandardRenamed_IsRenamed_AndStandardIsNot()
        {
            CadDocument document = Blank();
            document.Database.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = "GHS", FileName = "txt" });
            CadCommandSession session = Session(document);

            Assert.Equal("nil", ModStyle(session, "(cons 2 \"Other\")", "Standard"));
            Assert.NotEqual("nil", ModStyle(session, "(cons 2 \"GHT\")", "GHS"));
            Assert.True(document.Database.TextStyleTable.Has("GHT"));
            Assert.False(document.Database.TextStyleTable.Has("GHS"));
        }

        #endregion

        #region Linetypes, dimension styles, applications, UCS, views, viewports

        private const string MakeLinetype =
            "(entmake '((0 . \"LTYPE\") (100 . \"AcDbSymbolTableRecord\") (100 . \"AcDbLinetypeTableRecord\") (2 . \"LT1\") (70 . 0) (3 . \"desc one\") (72 . 65) (73 . 2) (40 . 6.0) (49 . 4.0) (74 . 0) (49 . -2.0) (74 . 0)))";

        [Fact]
        public void ALinetypeGivenADescriptionLengthAndFirstDash_HasThem_AndIsRenamed()
        {
            CadCommandSession session = Session(Blank());
            V(session, MakeLinetype);

            Assert.NotEqual("nil", Mod(session, "LTYPE", "LT1", "(cons 3 \"desc two\")"));
            Assert.Equal("\"desc two\"", V(session, "(cdr (assoc 3 (tblsearch \"LTYPE\" \"LT1\")))"));
            Assert.NotEqual("nil", Mod(session, "LTYPE", "LT1", "(cons 40 8.0)"));
            Assert.Equal("8.0", V(session, "(cdr (assoc 40 (tblsearch \"LTYPE\" \"LT1\")))"));
            Assert.NotEqual("nil", Mod(session, "LTYPE", "LT1", "(cons 2 \"LT2\")"));
            Assert.True(session.Document.Database.LinetypeTable.Has("LT2"));
            Assert.False(session.Document.Database.LinetypeTable.Has("LT1"));
        }

        [Fact]
        public void ALinetypesFirstDashGivenAgain_IsTheNewLengthAndTheSecondIsAsItWas()
        {
            CadCommandSession session = Session(Blank());
            V(session, MakeLinetype);

            V(session, "(setq e (tblobjname \"LTYPE\" \"LT1\"))");
            Assert.NotEqual("nil", V(session, "(entmod (subst (cons 49 5.0) (assoc 49 (entget e)) (entget e)))"));

            // AutoCAD 2021 (e4): tblsearch shows (49 . 5.0) (49 . -2.0)
            Assert.Equal(
                "((49 . 5.0) (49 . -2.0))",
                V(session, "(vl-remove-if-not '(lambda (g) (= (car g) 49)) (tblsearch \"LTYPE\" \"LT1\"))")
            );
        }

        [Fact]
        public void TheContinuousLinetype_IsNotChangedNorRenamed()
        {
            CadCommandSession session = Session(Blank());

            Assert.Equal("nil", Mod(session, "LTYPE", "Continuous", "(cons 3 \"x\")"));
            Assert.Equal("nil", Mod(session, "LTYPE", "Continuous", "(cons 2 \"Solid\")"));
        }

        [Fact]
        public void ADimensionStyleGivenAScale_HasIt_AndStandardIsNotRenamed()
        {
            CadCommandSession session = Session(Blank());

            Assert.NotEqual("nil", Mod(session, "DIMSTYLE", "Standard", "(cons 40 2.0)"));
            Assert.Equal("2.0", V(session, "(cdr (assoc 40 (tblsearch \"DIMSTYLE\" \"Standard\")))"));
            Assert.Equal("nil", Mod(session, "DIMSTYLE", "Standard", "(cons 2 \"Other\")"));
        }

        [Fact]
        public void AnApplicationsFlagsAreLeftAsTheyAre_AndItIsNotRenamed()
        {
            CadCommandSession session = Session(Blank());
            V(session, "(regapp \"GHAPP\")");

            Assert.NotEqual("nil", Mod(session, "APPID", "GHAPP", "(cons 70 1)"));
            Assert.Equal("0", V(session, "(cdr (assoc 70 (tblsearch \"APPID\" \"GHAPP\")))"));
            Assert.Equal("nil", Mod(session, "APPID", "GHAPP", "(cons 2 \"OTHER\")"));
        }

        [Fact]
        public void AViewGivenANewNameAndHeight_HasThem()
        {
            CadCommandSession session = Session(Blank());
            V(
                session,
                "(entmake (quote ((0 . \"VIEW\") (100 . \"AcDbSymbolTableRecord\") (100 . \"AcDbViewTableRecord\") (2 . \"V1\") (70 . 0) (40 . 100.0) (10 0.0 0.0) (41 . 200.0) (11 0.0 0.0 1.0) (12 0.0 0.0 0.0) (42 . 50.0) (43 . 0.0) (44 . 0.0) (50 . 0.0) (71 . 0))))"
            );

            Assert.NotEqual("nil", Mod(session, "VIEW", "V1", "(cons 40 77.0)"));
            Assert.Equal("77.0", V(session, "(cdr (assoc 40 (tblsearch \"VIEW\" \"V1\")))"));
            Assert.NotEqual("nil", Mod(session, "VIEW", "V1", "(cons 2 \"V2\")"));
            Assert.Equal("nil", V(session, "(tblsearch \"VIEW\" \"V1\")"));
            Assert.NotEqual("nil", V(session, "(tblsearch \"VIEW\" \"V2\")"));
        }

        [Fact]
        public void AUcsGivenAnOriginAndANewName_HasThem()
        {
            CadCommandSession session = Session(Blank());
            V(
                session,
                "(entmake (quote ((0 . \"UCS\") (100 . \"AcDbSymbolTableRecord\") (100 . \"AcDbUCSTableRecord\") (2 . \"U1\") (70 . 0) (10 0.0 0.0 0.0) (11 1.0 0.0 0.0) (12 0.0 1.0 0.0) (79 . 0) (146 . 0.0))))"
            );

            Assert.NotEqual("nil", Mod(session, "UCS", "U1", "(list 10 5.0 6.0 0.0)"));
            Assert.Equal("(5.0 6.0 0.0)", V(session, "(cdr (assoc 10 (tblsearch \"UCS\" \"U1\")))"));
            Assert.NotEqual("nil", Mod(session, "UCS", "U1", "(cons 2 \"U2\")"));
            Assert.NotEqual("nil", V(session, "(tblsearch \"UCS\" \"U2\")"));
        }

        [Fact]
        public void TheActiveViewport_IsNotChanged()
        {
            CadCommandSession session = Session(Blank());
            string before = session.Document.Database.ToText();

            Assert.Equal("nil", Mod(session, "VPORT", "*Active", "(cons 40 55.0)"));
            Assert.Equal(before, session.Document.Database.ToText());
        }

        #endregion

        #region XRECORDs

        private static CadCommandSession WithXrecord()
        {
            CadCommandSession session = Session(Blank());
            V(
                session,
                "(setq x (entmakex '((0 . \"XRECORD\") (100 . \"AcDbXrecord\") (280 . 1) (1 . \"a\") (70 . 5))))"
            );
            V(session, "(dictadd (namedobjdict) \"GHX\" x)");
            return session;
        }

        // The data of the record after its flag, as a list.
        private static string Data(CadCommandSession session) =>
            V(session, "(cdr (cdr (member '(100 . \"AcDbXrecord\") (entget x))))");

        [Fact]
        public void AnXrecordsListGivenBackWithOneGroupChanged_HasTheWholeDataAddedAfterTheOld()
        {
            CadCommandSession session = WithXrecord();
            Run(session, "REGEN");

            Assert.NotEqual("nil", V(session, "(entmod (subst (cons 1 \"b\") (assoc 1 (entget x)) (entget x)))"));

            // AutoCAD 2021 (e3): the data is not replaced but added to
            Assert.Equal("((1 . \"a\") (70 . 5) (1 . \"b\") (70 . 5))", Data(session));

            Run(session, "U");

            Assert.Equal("((1 . \"a\") (70 . 5))", Data(session));
        }

        [Fact]
        public void GroupsAddedToAnXrecordsList_ComeAfterTheDataItHas_AndTheFlagChangesInPlace()
        {
            CadCommandSession session = WithXrecord();

            V(
                session,
                "(entmod (list (cons -1 x) '(0 . \"XRECORD\") '(100 . \"AcDbXrecord\") '(280 . 0) '(1 . \"only\")))"
            );

            Assert.Equal("((1 . \"a\") (70 . 5) (1 . \"only\"))", Data(session));
            Assert.Equal("0", V(session, "(cdr (assoc 280 (entget x)))"));
        }

        [Fact]
        public void AGroupNoXrecordHolds_IsRefusedAndChangesNothing()
        {
            CadCommandSession session = WithXrecord();

            // AutoCAD 2021 (e3) raises "bad DXF group: (70 . \"s\")"; a list with a value of the wrong kind is nil here
            Assert.Equal(
                "nil",
                V(
                    session,
                    "(entmod (list (cons -1 x) (quote (0 . \"XRECORD\")) (quote (100 . \"AcDbXrecord\")) (quote (70 . \"s\"))))"
                )
            );
            Assert.Equal("((1 . \"a\") (70 . 5))", Data(session));
        }

        #endregion

        #region VERTEX

        private static CadCommandSession WithPolyline()
        {
            CadCommandSession session = Session(Blank());
            V(session, "(vl-load-com)");
            V(session, "(entmake '((0 . \"POLYLINE\") (66 . 1) (10 0.0 0.0 0.0) (70 . 0)))");
            V(session, "(entmake '((0 . \"VERTEX\") (10 0.0 0.0 0.0)))");
            V(session, "(entmake '((0 . \"VERTEX\") (10 10.0 0.0 0.0)))");
            V(session, "(entmake '((0 . \"VERTEX\") (10 10.0 10.0 0.0)))");
            V(session, "(entmake '((0 . \"SEQEND\")))");
            V(session, "(setq pl (entlast) v (entnext pl))");
            return session;
        }

        [Fact]
        public void AVertexOfAnOldStylePolyline_MovesToTheNewPointAtOnce()
        {
            CadCommandSession session = WithPolyline();

            string answer = V(session, "(entmod (subst (cons 10 '(12.0 0.0 0.0)) (assoc 10 (entget v)) (entget v)))");

            Assert.NotEqual("nil", answer);
            Assert.Equal("(12.0 0.0 0.0)", V(session, "(cdr (assoc 10 (entget v)))"));
            Assert.Equal("(12.0 0.0 0.0)", V(session, "(vlax-curve-getStartPoint pl)"));
        }

        [Fact]
        public void AVertexGivenABulgeAndAStartWidth_KeepsThem_AndOneUndoTakesTheMoveBack()
        {
            CadCommandSession session = WithPolyline();
            Run(session, "REGEN");

            V(session, "(entmod (append (entget v) '((40 . 2.0) (42 . 0.5))))");

            Assert.Equal("2.0", V(session, "(cdr (assoc 40 (entget v)))"));
            Assert.Equal("0.5", V(session, "(cdr (assoc 42 (entget v)))"));

            Run(session, "U");

            Assert.Equal("(42 . 0.0)", V(session, "(assoc 42 (entget v))"));
        }

        [Fact]
        public void TheSeqendOfAPolyline_IsNotChanged()
        {
            CadCommandSession session = WithPolyline();
            V(session, "(setq se (entnext (entnext (entnext v))))");

            Assert.Equal("\"SEQEND\"", V(session, "(cdr (assoc 0 (entget se)))"));
            Assert.Equal("nil", V(session, "(entmod (entget se))"));
        }

        #endregion

        #region entdel and the extension dictionary

        // A line with an extension dictionary of one XRECORD, made by vlax-ldata-put, and the name of that dictionary.
        private static CadCommandSession LineWithDictionary()
        {
            CadCommandSession session = Session(Blank());
            Run(session, "LINE", "0,0", "10,0", Enter);
            V(session, "(vl-load-com)");
            V(session, "(setq e (entlast))");
            V(session, "(vlax-ldata-put e \"K\" \"V\")");
            V(session, "(setq d (cdr (assoc 360 (entget e))))");
            return session;
        }

        [Fact]
        public void ALineErasedByEntdel_LeavesItsExtensionDictionaryReadable()
        {
            CadCommandSession session = LineWithDictionary();

            V(session, "(entdel e)");

            // AutoCAD 2021 (d1): the dictionary stays, its owner the erased line; the line's own data cannot be read
            Assert.Equal("nil", V(session, "(entget e)"));
            Assert.Contains("(0 . \"DICTIONARY\")", V(session, "(entget d)"));
            Assert.Contains("(3 . \"K\")", V(session, "(entget d)"));
            Assert.Equal("nil", V(session, "(vlax-ldata-get e \"K\")"));
        }

        [Fact]
        public void ALineBroughtBackByEntdel_HasTheSameExtensionDictionaryAndItsDataBack()
        {
            CadCommandSession session = LineWithDictionary();
            V(session, "(entdel e)");

            V(session, "(entdel e)");

            Assert.NotEqual("nil", V(session, "(entget e)"));
            Assert.Equal("\"V\"", V(session, "(vlax-ldata-get e \"K\")"));
            Assert.Equal("T", V(session, "(if (equal (cdr (assoc 360 (entget e))) d) T)"));
            Assert.Contains("(0 . \"DICTIONARY\")", V(session, "(entget d)"));
        }

        [Fact]
        public void ALineErasedByEntdelAndUndone_IsBackWithItsDictionary()
        {
            CadCommandSession session = LineWithDictionary();
            Run(session, "REGEN");

            V(session, "(entdel e)");
            Run(session, "U");

            Assert.Equal("\"V\"", V(session, "(vlax-ldata-get (entlast) \"K\")"));
        }

        #endregion
    }
}
