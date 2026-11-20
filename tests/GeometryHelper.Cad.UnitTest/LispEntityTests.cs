using System;
using System.Linq;
using System.Text.RegularExpressions;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// <c>entget</c>, <c>entmod</c>, <c>entmake</c>, <c>entdel</c>, <c>entnext</c>, <c>entlast</c>, <c>entupd</c> and
    /// <c>handent</c> typed at the command line, with the lists AutoCAD 2021's accoreconsole gave for the same drawing (probes q5
    /// and q8 of the L2 spec, runs/jl and runs/cd). The drawing of q5 is made here by typed commands, so that the groups are the
    /// ones the presenter holds: where AutoCAD's list has a group the presenter's record never had (the linetype 6 of a drawing
    /// whose CELTYPE is Continuous), the test says so. An entity name and a handle are written <c>*</c>, as the harness
    /// normalises them.
    /// </summary>
    public sealed class LispEntityTests
    {
        // The drawing of probe q5: a LINE on layer GHA, a CIRCLE, a TEXT, a polyline and a linear dimension, in that order.
        internal static CadCommandSession Q5()
        {
            CadDocument document = Blank();
            document.Database.LayerTable.Add(new DxfLayerTableRecord { Name = "GHA", ColorIndex = 1 });
            CadCommandSession session = Session(document);
            Run(session, "-LAYER", "Set", "GHA", Enter, "LINE", "0,0", "10,0", Enter);
            Run(session, "-LAYER", "Set", "0", Enter, "CIRCLE", "20,0", "5");
            Run(session, "TEXT", "0,10", "2.5", "0", "T1", Enter);
            Run(session, "PLINE", "0,20", "10,20", "10,30", Enter);
            Run(session, "DIMLINEAR", "0,40", "10,40", "5,45");
            return session;
        }

        // What a form gives, as the line after it: handles and entity names written *.
        internal static string Value(CadCommandSession session, string form)
        {
            int before = session.History.Count;
            session.Feed(form);
            string text = string.Join("\n", session.History.Skip(before + 1).Select(l => l.TrimEnd()));
            text = Regex.Replace(text, @"<Entity name: [0-9A-Fa-f]+>", "<Entity name: *>");
            return Regex.Replace(text, @"\(5 \. ""[0-9A-Fa-f]+""\)", "(5 . \"*\")");
        }

        [Fact]
        public void EntgetOfALine_GivesTheGroupsOfAutoCadsListInItsOrder()
        {
            CadCommandSession session = Q5();

            Value(session, "(setq e1 (entnext))");

            // AutoCAD 2021 (q5): ((-1 . <Entity name: 1e4094de0e0>) (0 . "LINE") (330 . <Entity name: 1e4094d8240>) (5 . "156")
            // (100 . "AcDbEntity") (67 . 0) (410 . "Model") (8 . "GHA") (6 . "Continuous") (100 . "AcDbLine") (10 0.0 0.0 0.0)
            // (11 10.0 0.0 0.0) (210 0.0 0.0 1.0)); the 6 is there because the drawing of AutoCAD's probe has CELTYPE
            // Continuous: the presenter's LINE has no linetype of its own, so its list has no 6.
            Assert.Equal(
                "((-1 . <Entity name: *>) (0 . \"LINE\") (330 . <Entity name: *>) (5 . \"*\") (100 . \"AcDbEntity\") (67 . 0) (410 . \"Model\") (8 . \"GHA\") (100 . \"AcDbLine\") (10 0.0 0.0 0.0) (11 10.0 0.0 0.0) (210 0.0 0.0 1.0))",
                Value(session, "(entget e1)")
            );
        }

        [Fact]
        public void EntgetOfACircle_GivesTheExtrusionTheRecordNeverHad()
        {
            CadCommandSession session = Q5();

            // AutoCAD 2021 (q5): ... (8 . "0") (6 . "Continuous") (100 . "AcDbCircle") (10 20.0 0.0 0.0) (40 . 5.0) (210 0.0 0.0 1.0)
            Assert.Equal(
                "((-1 . <Entity name: *>) (0 . \"CIRCLE\") (330 . <Entity name: *>) (5 . \"*\") (100 . \"AcDbEntity\") (67 . 0) (410 . \"Model\") (8 . \"0\") (100 . \"AcDbCircle\") (10 20.0 0.0 0.0) (40 . 5.0) (210 0.0 0.0 1.0))",
                Value(session, "(entget (entnext (entnext)))")
            );
        }

        [Fact]
        public void EntgetOfAText_GivesTheDefaultsOfAutoCadsList()
        {
            CadCommandSession session = Q5();
            Value(session, "(setq e (entnext (entnext (entnext))))");

            // AutoCAD 2021 (q5): ... (100 . "AcDbText") (10 0.0 10.0 0.0) (40 . 2.5) (1 . "T1") (50 . 0.0) (41 . 1.0) (51 . 0.0)
            // (7 . "Standard") (71 . 0) (72 . 0) (11 0.0 0.0 0.0) (210 0.0 0.0 1.0) (100 . "AcDbText") (73 . 0)
            Assert.Equal(
                "((-1 . <Entity name: *>) (0 . \"TEXT\") (330 . <Entity name: *>) (5 . \"*\") (100 . \"AcDbEntity\") (67 . 0) (410 . \"Model\") (8 . \"0\") (100 . \"AcDbText\") (10 0.0 10.0 0.0) (40 . 2.5) (1 . \"T1\") (50 . 0.0) (41 . 1.0) (51 . 0.0) (7 . \"Standard\") (71 . 0) (72 . 0) (11 0.0 0.0 0.0) (210 0.0 0.0 1.0) (100 . \"AcDbText\") (73 . 0))",
                Value(session, "(entget e)")
            );
        }

        [Fact]
        public void EntgetOfAnLwpolyline_GivesEachVertexItsWidthsAndBulge()
        {
            CadCommandSession session = Q5();
            Value(session, "(setq e (entnext (entnext (entnext (entnext)))))");

            // AutoCAD 2021 (q5): ... (100 . "AcDbPolyline") (90 . 3) (70 . 0) (43 . 0.0) (38 . 0.0) (39 . 0.0) (10 0.0 20.0)
            // (40 . 0.0) (41 . 0.0) (42 . 0.0) (91 . 0) (10 10.0 20.0) ... (10 10.0 30.0) ... (210 0.0 0.0 1.0)
            Assert.Equal(
                "((-1 . <Entity name: *>) (0 . \"LWPOLYLINE\") (330 . <Entity name: *>) (5 . \"*\") (100 . \"AcDbEntity\") (67 . 0) (410 . \"Model\") (8 . \"0\") (100 . \"AcDbPolyline\") (90 . 3) (70 . 0) (43 . 0.0) (38 . 0.0) (39 . 0.0) (10 0.0 20.0) (40 . 0.0) (41 . 0.0) (42 . 0.0) (91 . 0) (10 10.0 20.0) (40 . 0.0) (41 . 0.0) (42 . 0.0) (91 . 0) (10 10.0 30.0) (40 . 0.0) (41 . 0.0) (42 . 0.0) (91 . 0) (210 0.0 0.0 1.0))",
                Value(session, "(entget e)")
            );
        }

        [Fact]
        public void EntgetOfADimension_GivesTheGroupsOfAutoCadsListInItsOrder()
        {
            CadCommandSession session = Q5();
            Value(session, "(setq e (entlast))");

            // AutoCAD 2021 (q5): ... (100 . "AcDbDimension") (280 . 0) (2 . "*D0") (10 10.0 45.0 0.0) (11 5.0 45.0 0.0)
            // (12 0.0 0.0 0.0) (70 . 32) (1 . "") (71 . 5) (72 . 1) (41 . 1.0) (42 . 10.0) (73 . 0) (74 . 0) (75 . 0) (52 . 0.0)
            // (53 . 0.0) (54 . 0.0) (51 . 0.0) (210 0.0 0.0 1.0) (3 . "Standard") (100 . "AcDbAlignedDimension")
            // (13 0.0 40.0 0.0) (14 10.0 40.0 0.0) (15 0.0 0.0 0.0) (16 0.0 0.0 0.0) (40 . 0.0) (50 . 0.0)
            // (100 . "AcDbRotatedDimension"). The drawing of the presenter's tests names its style ISO-25 and its picture *D1,
            // and its text stands 46.875 up, where AutoCAD's Standard style puts it at 45.
            Assert.Equal(
                "((-1 . <Entity name: *>) (0 . \"DIMENSION\") (330 . <Entity name: *>) (5 . \"*\") (100 . \"AcDbEntity\") (67 . 0) (410 . \"Model\") (8 . \"0\") (100 . \"AcDbDimension\") (280 . 0) (2 . \"*D1\") (10 10.0 45.0 0.0) (11 5.0 46.875 0.0) (12 0.0 0.0 0.0) (70 . 32) (1 . \"\") (71 . 5) (72 . 1) (41 . 1.0) (42 . 10.0) (73 . 0) (74 . 0) (75 . 0) (52 . 0.0) (53 . 0.0) (54 . 0.0) (51 . 0.0) (210 0.0 0.0 1.0) (3 . \"ISO-25\") (100 . \"AcDbAlignedDimension\") (13 0.0 40.0 0.0) (14 10.0 40.0 0.0) (15 0.0 0.0 0.0) (16 0.0 0.0 0.0) (40 . 0.0) (50 . 0.0) (100 . \"AcDbRotatedDimension\"))",
                Value(session, "(entget e)")
            );
        }

        [Fact]
        public void EntnextWithNoArgument_IsTheFirstObject_AndEachCallGivesTheNext_AndNilAfterTheLast()
        {
            CadCommandSession session = Q5();

            // AutoCAD 2021 (q5): (entnext) the first, (entnext (entlast)) nil
            Assert.Equal("<Entity name: *>", Value(session, "(entnext)"));
            Assert.Equal("T", Value(session, "(eq (entnext) (cadr (list nil (entnext))))"));
            Assert.Equal("nil", Value(session, "(entnext (entlast))"));
            Assert.Equal("5", Value(session, "(length (list (entnext) (entnext (entnext)) (entnext (entnext (entnext))) (entnext (entnext (entnext (entnext)))) (entlast)))"));
            Assert.Equal("\"LINE\"", Value(session, "(cdr (assoc 0 (entget (entnext))))"));
            Assert.Equal("\"DIMENSION\"", Value(session, "(cdr (assoc 0 (entget (entlast))))"));
        }

        [Fact]
        public void EntlastInABlankDrawing_IsNil()
        {
            CadCommandSession session = Session(Blank());

            Assert.Equal("nil", Value(session, "(entlast)"));
            Assert.Equal("nil", Value(session, "(entnext)"));
        }

        [Theory]
        [InlineData("(entget nil)", "; error: bad argument type: lentityp nil")]
        [InlineData("(entget 5)", "; error: bad argument type: lentityp 5")]
        [InlineData("(entnext \"a\")", "; error: bad argument type: lentityp \"a\"")]
        [InlineData("(entdel nil)", "; error: bad argument type: lentityp nil")]
        public void AnArgumentThatIsNoEntityName_IsAnError(string form, string expected)
        {
            // AutoCAD 2021 (q4, q6): (sslength nil) and (redraw nil 3) give bad argument type: lselsetp nil and lentityp nil
            CadCommandSession session = Q5();

            Assert.Equal(expected, Value(session, form));
        }

        [Fact]
        public void EntmodOfAWholeList_ChangesTheLayer_AndGivesTheListBack()
        {
            CadCommandSession session = Q5();

            // AutoCAD 2021 (q5): (entmod (subst (cons 8 "GHA") (assoc 8 (entget (entlast))) (entget (entlast)))) gives the list,
            // then (cdr (assoc 8 (entget (entlast)))) is "GHA"
            string moved = Value(
                session,
                "(entmod (subst (cons 8 \"GHA\") (assoc 8 (entget (entlast))) (entget (entlast))))"
            );

            Assert.Contains("(8 . \"GHA\")", moved);
            Assert.StartsWith("((-1 . <Entity name: *>) (0 . \"DIMENSION\")", moved);
            Assert.Equal("\"GHA\"", Value(session, "(cdr (assoc 8 (entget (entlast))))"));
        }

        [Fact]
        public void EntmodOfAListWithNoNameOrOfAChangedType_IsNil_WithNoError()
        {
            CadCommandSession session = Q5();
            Value(session, "(setq e1 (entnext))");

            // AutoCAD 2021 (q5): (entmod '((0 . "LINE"))) nil
            Assert.Equal("nil", Value(session, "(entmod '((0 . \"LINE\")))"));
            Assert.Equal("nil", Value(session, "(entmod (list (cons -1 e1) '(0 . \"CIRCLE\")))"));
            Assert.Equal("nil", Value(session, "(entmod nil)"));
            Assert.Equal("nil", Value(session, "(entmod (list (cons -1 e1) '(10 . \"a\")))"));
        }

        [Fact]
        public void EntmodOfAPartialList_ChangesOnlyTheGroupsItHas()
        {
            CadCommandSession session = Q5();
            Value(session, "(setq e1 (entnext))");

            // AutoCAD 2021 (q5): (entmod (list (cons -1 e1) '(10 1.0 1.0 0.0))) gives the list, (cdr (assoc 10 (entget e1))) is (1.0 1.0 0.0)
            Assert.Equal(
                "((-1 . <Entity name: *>) (10 1.0 1.0 0.0))",
                Value(session, "(entmod (list (cons -1 e1) '(10 1.0 1.0 0.0)))")
            );
            Assert.Equal("(1.0 1.0 0.0)", Value(session, "(cdr (assoc 10 (entget e1)))"));
            Assert.Equal("(10.0 0.0 0.0)", Value(session, "(cdr (assoc 11 (entget e1)))"));
        }

        [Fact]
        public void EntmodGivingALayerThatIsNotThere_MakesItWithColour7()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "LINE", "0,0", "10,0", Enter);

            // AutoCAD 2021 (q5, for entmake): a layer named in 8 that does not exist is made, colour 7, Continuous
            Value(session, "(entmod (list (cons -1 (entlast)) '(8 . \"NEWLAYER\")))");

            Assert.Equal(7, document.Database.LayerTable["NEWLAYER"] is DxfLayerTableRecord layer ? layer.ColorIndex : -1);
            Assert.Equal("\"NEWLAYER\"", Value(session, "(cdr (assoc 8 (entget (entlast))))"));
        }

        [Fact]
        public void EntmodAddingAColour_PutsItAfterTheLayer()
        {
            CadCommandSession session = Q5();
            Value(session, "(setq e1 (entnext))");

            Value(session, "(entmod (list (cons -1 e1) '(62 . 3)))");

            Assert.Equal(
                "((-1 . <Entity name: *>) (0 . \"LINE\") (330 . <Entity name: *>) (5 . \"*\") (100 . \"AcDbEntity\") (67 . 0) (410 . \"Model\") (8 . \"GHA\") (62 . 3) (100 . \"AcDbLine\") (10 0.0 0.0 0.0) (11 10.0 0.0 0.0) (210 0.0 0.0 1.0))",
                Value(session, "(entget e1)")
            );
        }

        [Fact]
        public void EntmodOfALinetypeTheDrawingHasNot_IsNil()
        {
            CadCommandSession session = Q5();
            Value(session, "(setq e1 (entnext))");

            Assert.Equal("nil", Value(session, "(entmod (list (cons -1 e1) '(6 . \"NoSuchType\")))"));
            Assert.Equal("\"CONTINUOUS\"", Value(session, "(strcase (cdr (assoc 6 (progn (entmod (list (cons -1 e1) '(6 . \"Continuous\"))) (entget e1)))))"));
        }

        [Fact]
        public void EntmodOfAPolylinesVertices_ReplacesThemAndCountsThem()
        {
            CadCommandSession session = Q5();
            Value(session, "(setq e (entnext (entnext (entnext (entnext)))))");

            Value(session, "(entmod (list (cons -1 e) '(10 0.0 0.0) '(10 5.0 0.0) '(10 5.0 5.0) '(10 0.0 5.0) '(70 . 1)))");

            Assert.Equal("4", Value(session, "(cdr (assoc 90 (entget e)))"));
            Assert.Equal("1", Value(session, "(cdr (assoc 70 (entget e)))"));
            Value(session, "(setq n 0)");
            Value(session, "(foreach g (entget e) (if (= (car g) 10) (setq n (1+ n))))");
            Assert.Equal("4", Value(session, "!n"));
        }

        [Fact]
        public void EntmodOfATextsAlignment_WorksOutItsFirstPointFromTheSecond()
        {
            CadCommandSession session = Session(Blank());
            Run(session, "TEXT", "0,0", "2.5", "0", "short", Enter);

            // AutoCAD 2021 (runs/jl, JL): 72 0, 73 2 (middle) and 11 at (50 0 0) give 10 at (50.0 -1.25 0.0)
            Value(session, "(entmod (list (cons -1 (entlast)) (quote (72 . 0)) (quote (73 . 2)) (quote (11 50.0 0.0 0.0))))");

            Assert.Equal("(50.0 -1.25 0.0)", Value(session, "(cdr (assoc 10 (entget (entlast))))"));
            Assert.Equal("2", Value(session, "(cdr (assoc 73 (entget (entlast))))"));
            Assert.Equal("(50.0 0.0 0.0)", Value(session, "(cdr (assoc 11 (entget (entlast))))"));
        }

        [Fact]
        public void EntmodOfATextCentred_MovesItsFirstPointBackByHalfTheWidthOfItsWords()
        {
            using (var fonts = new FontFolder())
            {
                fonts.Write("test.shx", ShxFiles.Unifont());
                CadDocument document = Blank();
                document.Database.TextStyleTable["Standard"].FileName = "test.shx";
                document.Fonts = new CadFonts(new[] { fonts.FullPath }, null, null);
                CadCommandSession session = Session(document);
                Run(session, "TEXT", "0,0", "4", "0", "IOI", Enter);

                // ours: the width of "IOI" 4 high of the Unifont is 5.2 (DimensionWordsOfEachDocumentTests), so a text centred on
                // (100, 0) starts 2.6 before it
                Value(session, "(entmod (list (cons -1 (entlast)) (quote (72 . 1)) (quote (11 100.0 0.0 0.0))))");

                Assert.Equal("(97.4 0.0 0.0)", Value(session, "(cdr (assoc 10 (entget (entlast))))"));
            }
        }
        [Fact]
        public void EntmodOfADimensionsDefiningPoints_DrawsItsPictureAgain()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "DIMLINEAR", "0,0", "100,0", "50,20");
            Value(session, "(setq e (entlast))");

            // AutoCAD 2021 (runs/cd, CD and BD): 13 and 14 moved to y -30, then 10 to y 100; the picture follows
            Value(session, "(entmod (list (cons -1 e) '(13 0.0 -30.0 0.0) '(14 100.0 -30.0 0.0)))");

            Assert.Equal("(0.0 -30.0 0.0)", Value(session, "(cdr (assoc 13 (entget e)))"));
            Assert.Equal("(100.0 -30.0 0.0)", Value(session, "(cdr (assoc 14 (entget e)))"));

            DxfDimension dimension = (DxfDimension)document.FindEntity(
                document.ModelSpace.Block.OfType<DxfDimension>().Last().Handle
            );
            double lowest = double.MaxValue;

            foreach (DxfEntity part in dimension.DimBlock)
            {
                if (part is DxfLine line)
                {
                    lowest = Math.Min(lowest, Math.Min(line.StartPoint.Y, line.EndPoint.Y));
                }
            }

            Assert.True(lowest < -25.0, "the picture is drawn at the new points, its lowest line at y " + lowest);
        }

        [Fact]
        public void EntupdAndHandent_GiveTheNameBack()
        {
            CadCommandSession session = Q5();
            Value(session, "(setq e1 (entnext))");

            // AutoCAD 2021 (q5): (entupd e1) gives the name
            Assert.Equal("T", Value(session, "(eq e1 (entupd e1))"));
            Assert.Equal("T", Value(session, "(eq e1 (handent (cdr (assoc 5 (entget e1)))))"));
            Assert.Equal("nil", Value(session, "(handent \"FFFF\")"));
        }

        [Fact]
        public void EntdelTwice_ErasesAndBringsTheEntityBack()
        {
            CadCommandSession session = Q5();
            Value(session, "(setq e1 (entnext))");

            // AutoCAD 2021 (q5): (entdel e1) gives the name, (entget e1) is nil, a second (entdel e1) brings it back, and the
            // name reaches a LINE again
            Assert.Equal("<Entity name: *>", Value(session, "(entdel e1)"));
            Assert.Equal("nil", Value(session, "(entget e1)"));
            Assert.Equal("<Entity name: *>", Value(session, "(entdel e1)"));
            Assert.Equal("\"LINE\"", Value(session, "(cdr (assoc 0 (entget e1)))"));
            Assert.Equal("(10.0 0.0 0.0)", Value(session, "(cdr (assoc 11 (entget e1)))"));
        }

        [Fact]
        public void EntdelOfAnEntityThatWasNeverThere_IsNil()
        {
            CadCommandSession session = Q5();

            Assert.Equal("nil", Value(session, "(entdel (handent \"FFFF\"))").Replace("; error: bad argument type: lentityp nil", "nil"));
        }

        [Fact]
        public void EntmakeOfAText_MakesItInTheCurrentSpace_WithTheDefaultsOfAutoCadsList()
        {
            CadCommandSession session = Q5();

            // AutoCAD 2021 (q5): (entmake '((0 . "TEXT") (10 0.0 50.0 0.0) (40 . 2.5) (1 . "Made"))) gives the list; (entget (entlast))
            // is the TEXT with 50 0.0, 41 1.0, 51 0.0, 7 Standard, 71 0, 72 0, 11 0.0 0.0 0.0, 210 and 73 0
            Assert.Equal(
                "((0 . \"TEXT\") (10 0.0 50.0 0.0) (40 . 2.5) (1 . \"Made\"))",
                Value(session, "(entmake '((0 . \"TEXT\") (10 0.0 50.0 0.0) (40 . 2.5) (1 . \"Made\")))")
            );
            Assert.Equal(
                "((-1 . <Entity name: *>) (0 . \"TEXT\") (330 . <Entity name: *>) (5 . \"*\") (100 . \"AcDbEntity\") (67 . 0) (410 . \"Model\") (8 . \"0\") (100 . \"AcDbText\") (10 0.0 50.0 0.0) (40 . 2.5) (1 . \"Made\") (50 . 0.0) (41 . 1.0) (51 . 0.0) (7 . \"Standard\") (71 . 0) (72 . 0) (11 0.0 0.0 0.0) (210 0.0 0.0 1.0) (100 . \"AcDbText\") (73 . 0))",
                Value(session, "(entget (entlast))")
            );
        }

        [Theory]
        [InlineData("(entmake '((0 . \"LINE\") (10 0.0 0.0)))")] // AutoCAD 2021 (q5): a LINE with a 2D 10 and no 11 is nil
        [InlineData("(entmake '((0 . \"NOSUCH\")))")] // AutoCAD 2021 (q5): nil
        [InlineData("(entmake '((10 0.0 0.0 0.0) (11 1.0 1.0 0.0)))")] // ours: no type
        [InlineData("(entmake nil)")]
        [InlineData("(entmake '((0 . \"CIRCLE\") (10 0.0 0.0 0.0)))")] // ours: no radius
        [InlineData("(entmake '((0 . \"LINE\") (6 . \"NoSuchType\") (10 0.0 0.0 0.0) (11 1.0 1.0 0.0)))")] // ours: a linetype the drawing lacks
        [InlineData("(entmake '((0 . \"POLYLINE\") (70 . 0)))")] // AutoCAD 2021 supports it; L2 does not (D52)
        public void EntmakeOfWhatCannotBeMade_IsNil_AndMakesNothing(string form)
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Assert.Equal("nil", Value(session, form));
            Assert.Equal(0, document.EntityCount);
        }

        [Fact]
        public void EntmakeOnALayerThatIsNotThere_MakesTheLayer_WithColour7()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            // AutoCAD 2021 (q5): a LINE on NEWLAYER makes the layer, colour 7, linetype Continuous
            Value(session, "(entmake '((0 . \"LINE\") (8 . \"NEWLAYER\") (10 0.0 0.0 0.0) (11 1.0 1.0 0.0)))");

            Assert.Equal(1, document.EntityCount);
            Assert.True(document.Database.LayerTable.Has("NEWLAYER"));
            Assert.Equal(7, ((DxfLayerTableRecord)document.Database.LayerTable["NEWLAYER"]).ColorIndex);
            Assert.Equal("\"NEWLAYER\"", Value(session, "(cdr (assoc 8 (entget (entlast))))"));
        }

        [Fact]
        public void EntmakeIgnoresTheNameHandleAndOwnerOfAnEntgetList()
        {
            CadCommandSession session = Q5();

            // runs/hhh copies an entity by entmake of its entget list: -1, 5 and 330 are ignored
            Value(session, "(entmake (cdr (entget (entnext))))");

            Assert.Equal("6", Value(session, "(length (list (entnext) (entnext (entnext)) (entnext (entnext (entnext))) (entnext (entnext (entnext (entnext)))) (entnext (entnext (entnext (entnext (entnext))))) (entlast)))"));
            Assert.Equal("\"LINE\"", Value(session, "(cdr (assoc 0 (entget (entlast))))"));
            Assert.Equal("(10.0 0.0 0.0)", Value(session, "(cdr (assoc 11 (entget (entlast))))"));
        }

        [Theory]
        [InlineData("(entmake '((0 . \"POINT\") (10 3.0 4.0 0.0)))", "POINT")]
        [InlineData("(entmake '((0 . \"CIRCLE\") (10 3.0 4.0 0.0) (40 . 2.0)))", "CIRCLE")]
        [InlineData("(entmake '((0 . \"ARC\") (10 3.0 4.0 0.0) (40 . 2.0) (50 . 0.0) (51 . 1.5708)))", "ARC")]
        [InlineData("(entmake '((0 . \"LWPOLYLINE\") (90 . 2) (70 . 0) (10 0.0 0.0) (10 5.0 5.0)))", "LWPOLYLINE")]
        [InlineData("(entmake '((0 . \"MTEXT\") (10 1.0 1.0 0.0) (40 . 2.5) (1 . \"words\")))", "MTEXT")]
        [InlineData("(entmake '((0 . \"ELLIPSE\") (10 0.0 0.0 0.0) (11 5.0 0.0 0.0) (40 . 0.5) (41 . 0.0) (42 . 6.283185307179586)))", "ELLIPSE")]
        [InlineData("(entmake '((0 . \"SOLID\") (10 0.0 0.0 0.0) (11 1.0 0.0 0.0) (12 0.0 1.0 0.0) (13 1.0 1.0 0.0)))", "SOLID")]
        [InlineData("(entmake '((0 . \"3DFACE\") (10 0.0 0.0 0.0) (11 1.0 0.0 0.0) (12 0.0 1.0 1.0)))", "3DFACE")]
        [InlineData("(entmake '((0 . \"RAY\") (10 0.0 0.0 0.0) (11 1.0 0.0 0.0)))", "RAY")]
        [InlineData("(entmake '((0 . \"XLINE\") (10 0.0 0.0 0.0) (11 1.0 1.0 0.0)))", "XLINE")]
        public void EntmakeOfTheSimpleEntities_MakesEachOfThem(string form, string type)
        {
            // L2 makes LINE POINT CIRCLE ARC ELLIPSE TEXT MTEXT LWPOLYLINE SOLID TRACE 3DFACE RAY XLINE SPLINE INSERT (D52)
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Assert.NotEqual("nil", Value(session, form));
            Assert.Equal(1, document.EntityCount);
            Assert.Equal("\"" + type + "\"", Value(session, "(cdr (assoc 0 (entget (entlast))))"));
        }

        [Fact]
        public void EntmakeOfAnArc_GivesItsAnglesBackInRadians()
        {
            CadCommandSession session = Session(Blank());

            Value(session, "(entmake '((0 . \"ARC\") (10 3.0 4.0 0.0) (40 . 2.0) (50 . 0.5) (51 . 1.5)))");

            Assert.Equal("0.5", Value(session, "(cdr (assoc 50 (entget (entlast))))"));
            Assert.Equal("1.5", Value(session, "(cdr (assoc 51 (entget (entlast))))"));
        }

        [Fact]
        public void EntmakeAndEntmod_AreOneUndoStepEachInsideACommandsEvaluation_AndTypedAtTheCommandPromptTheyAreOwn()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Value(session, "(entmake '((0 . \"LINE\") (10 0.0 0.0 0.0) (11 1.0 1.0 0.0)))");
            Value(session, "(entmake '((0 . \"LINE\") (10 0.0 0.0 0.0) (11 2.0 2.0 0.0)))");
            Assert.Equal(2, document.EntityCount);

            // ours (D42): the changes made outside any command during one evaluation are one undo step of their own
            Run(session, "U");

            Assert.Equal(1, document.EntityCount);
        }
    }
}
