using System;
using System.Linq;
using System.Text.RegularExpressions;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// <c>entmake</c> of the complex entities (a polyline with its vertices and end, a block with its entities and end, a block
    /// reference with its attributes), of the records of the tables and of an ATTDEF, and what <c>tblsearch</c> gives for them
    /// (part E1 of l2p, decisions D1101 to D1106, D1108, D1110 and the obliquing of D1109). The expected values are those of
    /// AutoCAD 2021's accoreconsole in probe pa of scratch/l2p, marked <c>AutoCAD 2021 (console, l2p pa)</c>; what the probe did
    /// not measure is marked <c>ours</c>. An entity name is written <c>*</c>, as the harness of the routines normalises it.
    /// </summary>
    public sealed class LispEntmakeComplexTests : IDisposable
    {
        // Ends the routine a test left waiting, so no worker thread outlives its test.
        public void Dispose() => WaitingRoutines.EndAll();

        private static CadCommandSession Start() => Session(Blank());

        // What a form gives and writes, as the lines after its echo: entity names and handles written *.
        private static string Value(CadCommandSession session, string form)
        {
            int before = session.History.Count;
            session.Feed(form);
            string text = string.Join("\n", session.History.Skip(before + 1).Select(l => l.TrimEnd()));
            text = Regex.Replace(text, @"<Entity name: [0-9A-Fa-f]+>", "<Entity name: *>");
            return Regex.Replace(text, @"\(5 \. ""[0-9A-Fa-f]+""\)", "(5 . \"*\")");
        }

        private static string TypeOfLast(CadCommandSession session) =>
            Value(session, "(cdr (assoc 0 (entget (entlast))))");

        // The types of the objects an entity name starts: the entity, then what follows it in its space or block.
        private static string Chain(CadCommandSession session, string start) =>
            Value(
                session,
                "(progn (setq c nil e "
                    + start
                    + ") (while e (setq c (cons (cdr (assoc 0 (entget e))) c) e (entnext e))) (reverse c))"
            );

        private const string FirstPolyline = "(entmake '((0 . \"POLYLINE\") (66 . 1) (10 0.0 0.0 0.0) (70 . 0)))";

        private static void MakePolyline(CadCommandSession session, int vertices)
        {
            Value(session, FirstPolyline);

            for (int i = 0; i < vertices; i++)
            {
                Value(session, "(entmake '((0 . \"VERTEX\") (10 " + (i * 10) + ".0 0.0 0.0)))");
            }

            Value(session, "(entmake '((0 . \"SEQEND\")))");
        }

        // The block GHE1 of probe pa: a line and an attribute definition in it.
        private static void MakeBlockGhe1(CadCommandSession session)
        {
            Value(session, "(entmake '((0 . \"BLOCK\") (2 . \"GHE1\") (70 . 2) (10 0.0 0.0 0.0)))");
            Value(session, "(entmake '((0 . \"LINE\") (10 0.0 0.0 0.0) (11 10.0 0.0 0.0)))");
            Value(
                session,
                "(entmake '((0 . \"ATTDEF\") (10 0.0 2.0 0.0) (40 . 2.5) (1 . \"D\") (3 . \"Prompt\") (2 . \"TAG1\") (70 . 0)))"
            );
            Value(session, "(entmake '((0 . \"ENDBLK\")))");
        }

        #region POLYLINE, VERTEX and SEQEND

        [Fact]
        public void APolylineWithItsVerticesAndEnd_IsOneEntityOnlyOnceTheEndIsMade()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            // AutoCAD 2021 (console, l2p pa): each entmake gives its list, nothing is made and entlast is as it was (nil here)
            // until (entmake '((0 . "SEQEND"))), which gives ((0 . "SEQEND")); then entlast is the POLYLINE
            Assert.Equal("((0 . \"POLYLINE\") (66 . 1) (10 0.0 0.0 0.0) (70 . 0))", Value(session, FirstPolyline));
            Assert.Equal("nil", Value(session, "(entlast)"));
            Assert.Equal(0, document.EntityCount);
            Assert.Equal(
                "((0 . \"VERTEX\") (10 0.0 0.0 0.0))",
                Value(session, "(entmake '((0 . \"VERTEX\") (10 0.0 0.0 0.0)))")
            );
            Assert.Equal(
                "((0 . \"VERTEX\") (10 10.0 0.0 0.0) (42 . 0.5))",
                Value(session, "(entmake '((0 . \"VERTEX\") (10 10.0 0.0 0.0) (42 . 0.5)))")
            );
            Assert.Equal(
                "((0 . \"VERTEX\") (10 10.0 10.0 0.0) (40 . 1.0) (41 . 2.0))",
                Value(session, "(entmake '((0 . \"VERTEX\") (10 10.0 10.0 0.0) (40 . 1.0) (41 . 2.0)))")
            );
            Assert.Equal("nil", Value(session, "(entlast)"));
            Assert.Equal(0, document.EntityCount);
            Assert.Equal("((0 . \"SEQEND\"))", Value(session, "(entmake '((0 . \"SEQEND\")))"));

            Assert.Equal(1, document.EntityCount);
            Assert.Equal("\"POLYLINE\"", TypeOfLast(session));
            Assert.Equal("(\"POLYLINE\" \"VERTEX\" \"VERTEX\" \"VERTEX\" \"SEQEND\")", Chain(session, "(entlast)"));
        }

        [Fact]
        public void TheVerticesOfAMadePolyline_KeepTheirPointsBulgesAndWidths()
        {
            CadCommandSession session = Start();
            Value(session, FirstPolyline);
            Value(session, "(entmake '((0 . \"VERTEX\") (10 0.0 0.0 0.0)))");
            Value(session, "(entmake '((0 . \"VERTEX\") (10 10.0 0.0 0.0) (42 . 0.5)))");
            Value(session, "(entmake '((0 . \"VERTEX\") (10 10.0 10.0 0.0) (40 . 1.0) (41 . 2.0)))");
            Value(session, "(entmake '((0 . \"SEQEND\")))");
            Value(session, "(setq v1 (entnext (entlast)) v2 (entnext v1) v3 (entnext v2))");

            // AutoCAD 2021 (console, l2p pa): the bulge 42 and the widths 40 and 41 of a vertex are kept, a 2D vertex's point is
            // (10 x y z)
            Assert.Equal("(10.0 0.0 0.0)", Value(session, "(cdr (assoc 10 (entget v2)))"));
            Assert.Equal("0.5", Value(session, "(cdr (assoc 42 (entget v2)))"));
            Assert.Equal("1.0", Value(session, "(cdr (assoc 40 (entget v3)))"));
            Assert.Equal("2.0", Value(session, "(cdr (assoc 41 (entget v3)))"));
            Assert.Equal("(10.0 10.0 0.0)", Value(session, "(cdr (assoc 10 (entget v3)))"));
        }

        [Fact]
        public void AClosedPolyline_KeepsItsFlag()
        {
            CadCommandSession session = Start();
            Value(session, "(entmake '((0 . \"POLYLINE\") (66 . 1) (10 0.0 0.0 0.0) (70 . 1)))");
            Value(session, "(entmake '((0 . \"VERTEX\") (10 0.0 0.0 0.0)))");
            Value(session, "(entmake '((0 . \"VERTEX\") (10 5.0 0.0 0.0)))");
            Value(session, "(entmake '((0 . \"VERTEX\") (10 5.0 5.0 0.0)))");
            Value(session, "(entmake '((0 . \"SEQEND\")))");

            // AutoCAD 2021 (console, l2p pa, a closed flag 1 is kept): (70 . 1)
            Assert.Equal("1", Value(session, "(cdr (assoc 70 (entget (entlast))))"));
            Assert.Equal("(\"AcDbEntity\" \"AcDb2dPolyline\")", Value(session, SubclassMarkers));
        }

        private const string SubclassMarkers =
            "(mapcar 'cdr (vl-remove-if-not '(lambda (x) (= (car x) 100)) (entget (entlast))))";

        [Fact]
        public void APolylineWithFlag8_IsA3dPolylineOfVerticesThatSayFlag32()
        {
            CadCommandSession session = Start();

            // AutoCAD 2021 (console, l2p pa): (70 . 9) makes an AcDb3dPolyline, its vertices AcDb3dPolylineVertex with 70 32
            Value(session, "(entmake '((0 . \"POLYLINE\") (66 . 1) (10 0.0 0.0 0.0) (70 . 9)))");
            Value(session, "(entmake '((0 . \"VERTEX\") (10 0.0 0.0 0.0) (70 . 32)))");
            Value(session, "(entmake '((0 . \"VERTEX\") (10 10.0 0.0 5.0) (70 . 32)))");
            Value(session, "(entmake '((0 . \"VERTEX\") (10 10.0 10.0 5.0) (70 . 32)))");
            Assert.Equal("((0 . \"SEQEND\"))", Value(session, "(entmake '((0 . \"SEQEND\")))"));

            Assert.Equal("(\"AcDbEntity\" \"AcDb3dPolyline\")", Value(session, SubclassMarkers));
            Assert.Equal("9", Value(session, "(cdr (assoc 70 (entget (entlast))))"));
            Value(session, "(setq v (entnext (entnext (entlast))))");
            Assert.Equal("(10.0 0.0 5.0)", Value(session, "(cdr (assoc 10 (entget v)))"));
            Assert.Equal("32", Value(session, "(cdr (assoc 70 (entget v)))"));
            Assert.Equal(
                "(\"AcDbEntity\" \"AcDbVertex\" \"AcDb3dPolylineVertex\")",
                Value(session, "(mapcar 'cdr (vl-remove-if-not '(lambda (x) (= (car x) 100)) (entget v)))")
            );
        }

        [Fact]
        public void APolylineWithoutGroup66_IsMadeAllTheSame()
        {
            CadCommandSession session = Start();
            Value(session, "(entmake '((0 . \"POLYLINE\") (10 0.0 0.0 0.0)))");
            Value(session, "(entmake '((0 . \"VERTEX\") (10 1.0 1.0 0.0)))");
            Value(session, "(entmake '((0 . \"VERTEX\") (10 2.0 1.0 0.0)))");
            Value(session, "(entmake '((0 . \"SEQEND\")))");

            // AutoCAD 2021 (console, l2p pa): group 66 is not required
            Assert.Equal("\"POLYLINE\"", TypeOfLast(session));
        }

        [Fact]
        public void AnEndWithNoVertex_WritesInvalidPolylineObject_GivesNil_AndMakesNothing()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Value(session, FirstPolyline);

            // AutoCAD 2021 (console, l2p pa): "Invalid POLYLINE object." on a line of its own, then nil
            Assert.Equal("Invalid POLYLINE object.\nnil", Value(session, "(entmake '((0 . \"SEQEND\")))"));
            Assert.Equal(0, document.EntityCount);
            Assert.Equal("nil", Value(session, "(entlast)"));

            // and what was pending is gone: a new polyline starts afresh
            Assert.Equal("((0 . \"POLYLINE\") (66 . 1) (10 0.0 0.0 0.0) (70 . 0))", Value(session, FirstPolyline));
        }

        [Fact]
        public void OneVertexIsEnoughForAPolyline()
        {
            CadCommandSession session = Start();
            Value(session, FirstPolyline);
            Value(session, "(entmake '((0 . \"VERTEX\") (10 1.0 1.0 0.0)))");

            // AutoCAD 2021 (console, l2p pa)
            Assert.Equal("((0 . \"SEQEND\"))", Value(session, "(entmake '((0 . \"SEQEND\")))"));
            Assert.Equal("\"POLYLINE\"", TypeOfLast(session));
        }

        [Fact]
        public void AVertexOrAnEndWithNothingPending_GivesNilAndWritesNothing()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            // AutoCAD 2021 (console, l2p pa)
            Assert.Equal("nil", Value(session, "(entmake '((0 . \"VERTEX\") (10 1.0 1.0 0.0)))"));
            Assert.Equal("nil", Value(session, "(entmake '((0 . \"SEQEND\")))"));
            Assert.Equal(0, document.EntityCount);
        }

        [Fact]
        public void AnotherEntityWhileAPolylineIsPending_WritesInvalidComplexObject_IsNotMade_AndDropsWhatWasPending()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            MakePolyline(session, 2);
            Assert.Equal(1, document.EntityCount);
            Value(session, FirstPolyline);
            Value(session, "(entmake '((0 . \"VERTEX\") (10 1.0 1.0 0.0)))");

            // AutoCAD 2021 (console, l2p pa): "Invalid complex object." with no line break after it, so the console shows
            // "Invalid complex object.nil"; the line is not made; the next VERTEX and SEQEND give nil
            Assert.Equal(
                "Invalid complex object.nil",
                Value(session, "(entmake '((0 . \"LINE\") (10 0.0 0.0 0.0) (11 5.0 5.0 0.0)))")
            );
            Assert.Equal("nil", Value(session, "(entmake '((0 . \"VERTEX\") (10 2.0 1.0 0.0)))"));
            Assert.Equal("nil", Value(session, "(entmake '((0 . \"SEQEND\")))"));
            Assert.Equal(1, document.EntityCount);
            Assert.Equal("\"POLYLINE\"", TypeOfLast(session));
        }

        [Fact]
        public void AVertexWithNoPoint_GivesNilAndDropsWhatWasPending()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Value(session, FirstPolyline);
            Value(session, "(entmake '((0 . \"VERTEX\") (10 1.0 1.0 0.0)))");

            // AutoCAD 2021 (console, l2p pa): a VERTEX with no group 10 gives nil, silently, and the polyline is dropped
            Assert.Equal("nil", Value(session, "(entmake '((0 . \"VERTEX\")))"));
            Assert.Equal("nil", Value(session, "(entmake '((0 . \"VERTEX\") (10 2.0 1.0 0.0)))"));
            Assert.Equal("nil", Value(session, "(entmake '((0 . \"SEQEND\")))"));
            Assert.Equal(0, document.EntityCount);
        }

        [Fact]
        public void EntmakeWithNoArgument_GivesNilAndKeepsWhatIsPending()
        {
            CadCommandSession session = Start();
            Value(session, FirstPolyline);
            Value(session, "(entmake '((0 . \"VERTEX\") (10 1.0 1.0 0.0)))");

            // AutoCAD 2021 (console, l2p pa): (entmake) is nil and the SEQEND after it still made the polyline
            Assert.Equal("nil", Value(session, "(entmake)"));
            Assert.Equal("((0 . \"SEQEND\"))", Value(session, "(entmake '((0 . \"SEQEND\")))"));
            Assert.Equal("\"POLYLINE\"", TypeOfLast(session));
        }

        [Fact]
        public void APolylineMadeByEntmake_IsOneUndoStepAndOneUTakesItBack()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            MakePolyline(session, 2);
            Assert.Equal(1, document.EntityCount);

            // AutoCAD 2021 (console, l2p pa): U takes the polyline back whole, and entlast is nil again
            Run(session, "U");

            Assert.Equal(0, document.EntityCount);
            Assert.Equal("nil", Value(session, "(entlast)"));
        }

        [Fact]
        public void APolylineWithAPolyfaceFlag_IsMadeAsTheDxfModelTakesIt()
        {
            CadCommandSession session = Start();

            // ours (unmeasured, D1101): polyface (70 64) polylines are made as the DXF model takes them
            Value(session, "(entmake '((0 . \"POLYLINE\") (66 . 1) (10 0.0 0.0 0.0) (70 . 64) (71 . 3) (72 . 1)))");
            Value(session, "(entmake '((0 . \"VERTEX\") (10 0.0 0.0 0.0) (70 . 192)))");
            Value(session, "(entmake '((0 . \"VERTEX\") (10 5.0 0.0 0.0) (70 . 192)))");
            Value(session, "(entmake '((0 . \"VERTEX\") (10 5.0 5.0 0.0) (70 . 192)))");
            Value(session, "(entmake '((0 . \"VERTEX\") (10 0.0 0.0 0.0) (70 . 128) (71 . 1) (72 . 2) (73 . 3)))");
            Assert.Equal("((0 . \"SEQEND\"))", Value(session, "(entmake '((0 . \"SEQEND\")))"));

            Assert.Equal("64", Value(session, "(cdr (assoc 70 (entget (entlast))))"));
        }

        #endregion

        #region BLOCK and ENDBLK

        [Fact]
        public void ABlockMadeByEntmake_TakesTheEntitiesMadeAfterIt_AndEndblkGivesItsName()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            // AutoCAD 2021 (console, l2p pa): BLOCK gives its list, the entities made after it go into the definition and entlast
            // does not change; (entmake '((0 . "ENDBLK"))) gives the name "GHE1"
            Assert.Equal(
                "((0 . \"BLOCK\") (2 . \"GHE1\") (70 . 2) (10 0.0 0.0 0.0))",
                Value(session, "(entmake '((0 . \"BLOCK\") (2 . \"GHE1\") (70 . 2) (10 0.0 0.0 0.0)))")
            );
            Assert.Equal(
                "((0 . \"LINE\") (10 0.0 0.0 0.0) (11 10.0 0.0 0.0))",
                Value(session, "(entmake '((0 . \"LINE\") (10 0.0 0.0 0.0) (11 10.0 0.0 0.0)))")
            );
            Assert.Equal(
                "((0 . \"ATTDEF\") (10 0.0 2.0 0.0) (40 . 2.5) (1 . \"D\") (3 . \"Prompt\") (2 . \"TAG1\") (70 . 0))",
                Value(
                    session,
                    "(entmake '((0 . \"ATTDEF\") (10 0.0 2.0 0.0) (40 . 2.5) (1 . \"D\") (3 . \"Prompt\") (2 . \"TAG1\") (70 . 0)))"
                )
            );
            Assert.Equal("nil", Value(session, "(entlast)"));
            Assert.Equal(0, document.EntityCount);
            Assert.Equal("\"GHE1\"", Value(session, "(entmake '((0 . \"ENDBLK\")))"));

            Assert.Equal("nil", Value(session, "(entlast)"));
            Assert.Equal(0, document.EntityCount);
            Assert.True(document.Database.BlockTable.Has("GHE1"));
        }

        [Fact]
        public void TblsearchOfABlockMadeByEntmake_HasItsFlagsDescriptionBaseAndFirstEntity()
        {
            CadCommandSession session = Start();
            MakeBlockGhe1(session);

            // AutoCAD 2021 (console, l2p pa, D1108): the description (4 . "") follows the flags; 70 2 marks the attribute
            Assert.Equal(
                "((0 . \"BLOCK\") (2 . \"GHE1\") (70 . 2) (4 . \"\") (10 0.0 0.0 0.0) (-2 . <Entity name: *>))",
                Value(session, "(tblsearch \"BLOCK\" \"GHE1\")")
            );
            Assert.Equal("(\"LINE\" \"ATTDEF\")", Chain(session, "(cdr (assoc -2 (tblsearch \"BLOCK\" \"GHE1\")))"));
            Assert.Equal(
                "\"TAG1\"",
                Value(session, "(cdr (assoc 2 (entget (entnext (cdr (assoc -2 (tblsearch \"BLOCK\" \"GHE1\")))))))")
            );
        }

        [Fact]
        public void ABlockNamedStarUWithFlag1_IsAnonymous_AndEndblkGivesTheNameTheDrawingFound()
        {
            CadCommandSession session = Start();
            Value(session, "(entmake '((0 . \"BLOCK\") (2 . \"*U\") (70 . 1) (10 0.0 0.0 0.0)))");
            Value(session, "(entmake '((0 . \"LINE\") (10 0.0 0.0 0.0) (11 1.0 0.0 0.0)))");

            // AutoCAD 2021 (console, l2p pa): "*U1"; the next free number is ours
            Assert.Equal("\"*U1\"", Value(session, "(entmake '((0 . \"ENDBLK\")))"));
            Assert.Equal("1", Value(session, "(cdr (assoc 70 (tblsearch \"BLOCK\" \"*U1\")))"));
        }

        [Fact]
        public void ABlockWhoseNameExists_IsRedefined_AndEndblkWritesTheNoteAndGivesTheName()
        {
            CadCommandSession session = Start();
            MakeBlockGhe1(session);
            Value(session, "(entmake '((0 . \"BLOCK\") (2 . \"GHE1\") (70 . 0) (10 0.0 0.0 0.0)))");
            Value(session, "(entmake '((0 . \"CIRCLE\") (10 0.0 0.0 0.0) (40 . 3.0)))");

            // AutoCAD 2021 (console, l2p pa): Redefining block "GHE1" on its own line, then the name
            Assert.Equal("Redefining block \"GHE1\"\n\"GHE1\"", Value(session, "(entmake '((0 . \"ENDBLK\")))"));
            Assert.Equal("(\"CIRCLE\")", Chain(session, "(cdr (assoc -2 (tblsearch \"BLOCK\" \"GHE1\")))"));
        }

        [Fact]
        public void AnEmptyBlock_IsMade()
        {
            CadCommandSession session = Start();
            Value(session, "(entmake '((0 . \"BLOCK\") (2 . \"GHEMPTY\") (70 . 0) (10 0.0 0.0 0.0)))");

            // AutoCAD 2021 (console, l2p pa): the name; tblsearch has no -2 (the presenter's record gives -2 only with an entity)
            Assert.Equal("\"GHEMPTY\"", Value(session, "(entmake '((0 . \"ENDBLK\")))"));
            Assert.StartsWith(
                "((0 . \"BLOCK\") (2 . \"GHEMPTY\") (70 . 0) (4 . \"\") (10 0.0 0.0 0.0)",
                Value(session, "(tblsearch \"BLOCK\" \"GHEMPTY\")")
            );
        }

        [Fact]
        public void EndblkWithNothingPending_GivesNil()
        {
            // AutoCAD 2021 (console, l2p pa)
            Assert.Equal("nil", Value(Start(), "(entmake '((0 . \"ENDBLK\")))"));
        }

        [Fact]
        public void ABlockWithNoFlags_IsTaken_ButEndblkWritesBlockFlagsMissing_GivesNilAndMakesNothing()
        {
            CadCommandSession session = Start();

            // AutoCAD 2021 (console, l2p pa): the BLOCK gives its list, "Block flags missing" has no line break after it
            Assert.Equal(
                "((0 . \"BLOCK\") (2 . \"ghlow\") (10 0.0 0.0 0.0))",
                Value(session, "(entmake '((0 . \"BLOCK\") (2 . \"ghlow\") (10 0.0 0.0 0.0)))")
            );
            Assert.Equal(
                "((0 . \"POINT\") (10 0.0 0.0 0.0))",
                Value(session, "(entmake '((0 . \"POINT\") (10 0.0 0.0 0.0)))")
            );
            Assert.Equal("Block flags missingnil", Value(session, "(entmake '((0 . \"ENDBLK\")))"));
            Assert.Equal("nil", Value(session, "(tblsearch \"BLOCK\" \"GHLOW\")"));
        }

        [Fact]
        public void ABlockWithNoBasePoint_GivesNilAtOnce()
        {
            // AutoCAD 2021 (console, l2p pa)
            Assert.Equal("nil", Value(Start(), "(entmake '((0 . \"BLOCK\") (2 . \"GHNOPT\")))"));
        }

        [Fact]
        public void AnEntityInsideABlockThatCannotBeMade_GivesNil_AndDropsTheBlock()
        {
            CadCommandSession session = Start();
            Value(session, "(entmake '((0 . \"BLOCK\") (2 . \"GHBAD\") (10 0.0 0.0 0.0)))");

            // AutoCAD 2021 (console, l2p pa): a LINE with no second point gives nil, ENDBLK gives nil and no block is made
            Assert.Equal("nil", Value(session, "(entmake '((0 . \"LINE\") (10 0.0 0.0 0.0)))"));
            Assert.Equal("nil", Value(session, "(entmake '((0 . \"ENDBLK\")))"));
            Assert.Equal("nil", Value(session, "(tblsearch \"BLOCK\" \"GHBAD\")"));
        }

        [Fact]
        public void ABlockMadeByEntmake_IsOneUndoStepAndOneUTakesItBack()
        {
            CadCommandSession session = Start();
            MakeBlockGhe1(session);
            Assert.NotEqual("nil", Value(session, "(tblsearch \"BLOCK\" \"GHE1\")"));

            // ours: the made block joins the typed form's undo step as an entity does (D42), so U takes it back
            Run(session, "U");

            Assert.Equal("nil", Value(session, "(tblsearch \"BLOCK\" \"GHE1\")"));
        }

        [Fact]
        public void APolylineMadeInsideABlock_GoesIntoTheDefinition()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Value(session, "(entmake '((0 . \"BLOCK\") (2 . \"GHP\") (70 . 0) (10 0.0 0.0 0.0)))");
            Value(session, FirstPolyline);
            Value(session, "(entmake '((0 . \"VERTEX\") (10 1.0 1.0 0.0)))");
            Value(session, "(entmake '((0 . \"VERTEX\") (10 2.0 1.0 0.0)))");
            Value(session, "(entmake '((0 . \"SEQEND\")))");

            // ours (D1102 says the entities made after BLOCK go into the definition; a complex one is an entity)
            Assert.Equal(0, document.EntityCount);
            Assert.Equal("\"GHP\"", Value(session, "(entmake '((0 . \"ENDBLK\")))"));
            Assert.Equal(
                "(\"POLYLINE\" \"VERTEX\" \"VERTEX\" \"SEQEND\")",
                Chain(session, "(cdr (assoc -2 (tblsearch \"BLOCK\" \"GHP\")))")
            );
        }

        #endregion

        #region INSERT with attributes

        [Fact]
        public void AnInsertWithFlag66_TakesItsAttributesAndEndInOrder()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            MakeBlockGhe1(session);

            // AutoCAD 2021 (console, l2p pa): each gives its list, the end gives ((0 . "SEQEND")) and the reference with its
            // attribute appears, entlast the INSERT
            Assert.Equal(
                "((0 . \"INSERT\") (2 . \"GHE1\") (66 . 1) (10 20.0 0.0 0.0))",
                Value(session, "(entmake '((0 . \"INSERT\") (2 . \"GHE1\") (66 . 1) (10 20.0 0.0 0.0)))")
            );
            Assert.Equal(0, document.EntityCount);
            Assert.Equal(
                "((0 . \"ATTRIB\") (10 20.0 2.0 0.0) (40 . 2.5) (1 . \"V1\") (2 . \"TAG1\") (70 . 0))",
                Value(
                    session,
                    "(entmake '((0 . \"ATTRIB\") (10 20.0 2.0 0.0) (40 . 2.5) (1 . \"V1\") (2 . \"TAG1\") (70 . 0)))"
                )
            );
            Assert.Equal("((0 . \"SEQEND\"))", Value(session, "(entmake '((0 . \"SEQEND\")))"));

            Assert.Equal(1, document.EntityCount);
            Assert.Equal("\"INSERT\"", TypeOfLast(session));
            Assert.Equal("(\"INSERT\" \"ATTRIB\" \"SEQEND\")", Chain(session, "(entlast)"));
            Assert.Equal("(20.0 0.0 0.0)", Value(session, "(cdr (assoc 10 (entget (entlast))))"));
            Assert.Equal("\"V1\"", Value(session, "(cdr (assoc 1 (entget (entnext (entlast)))))"));
            Assert.Equal("\"TAG1\"", Value(session, "(cdr (assoc 2 (entget (entnext (entlast)))))"));
            Assert.Equal("(20.0 2.0 0.0)", Value(session, "(cdr (assoc 10 (entget (entnext (entlast)))))"));
        }

        [Fact]
        public void AnInsertWithFlag66AndNoAttribute_IsMadeAtTheEnd()
        {
            CadCommandSession session = Start();
            MakeBlockGhe1(session);
            Value(session, "(entmake '((0 . \"INSERT\") (2 . \"GHE1\") (66 . 1) (10 40.0 0.0 0.0)))");

            // AutoCAD 2021 (console, l2p pa)
            Assert.Equal("((0 . \"SEQEND\"))", Value(session, "(entmake '((0 . \"SEQEND\")))"));
            Assert.Equal("\"INSERT\"", TypeOfLast(session));
            Assert.Equal("(\"INSERT\")", Chain(session, "(entlast)").Replace(" \"SEQEND\"", string.Empty));
        }

        [Fact]
        public void AnAttributeWithNothingPending_GivesNil()
        {
            CadCommandSession session = Start();
            MakeBlockGhe1(session);

            // AutoCAD 2021 (console, l2p pa)
            Assert.Equal(
                "nil",
                Value(session, "(entmake '((0 . \"ATTRIB\") (10 20.0 2.0 0.0) (40 . 2.5) (1 . \"V1\") (2 . \"TAG1\")))")
            );
        }

        [Fact]
        public void AnotherEntityWhileAnInsertIsPending_WritesInvalidComplexObject_AndDropsTheInsert()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            MakeBlockGhe1(session);
            Value(session, "(entmake '((0 . \"INSERT\") (2 . \"GHE1\") (66 . 1) (10 20.0 0.0 0.0)))");

            // the refusals of D1101 apply to an INSERT with attributes (D1103)
            Assert.Equal(
                "Invalid complex object.nil",
                Value(session, "(entmake '((0 . \"LINE\") (10 0.0 0.0 0.0) (11 5.0 5.0 0.0)))")
            );
            Assert.Equal("nil", Value(session, "(entmake '((0 . \"SEQEND\")))"));
            Assert.Equal(0, document.EntityCount);
        }

        [Fact]
        public void AnAttributeWithNoTag_GivesNilAndDropsTheInsert()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            MakeBlockGhe1(session);
            Value(session, "(entmake '((0 . \"INSERT\") (2 . \"GHE1\") (66 . 1) (10 20.0 0.0 0.0)))");

            // ours (D1103: a bad ATTRIB is refused as D1101 refuses a bad VERTEX)
            Assert.Equal(
                "nil",
                Value(session, "(entmake '((0 . \"ATTRIB\") (10 20.0 2.0 0.0) (40 . 2.5) (1 . \"V1\")))")
            );
            Assert.Equal("nil", Value(session, "(entmake '((0 . \"SEQEND\")))"));
            Assert.Equal(0, document.EntityCount);
        }

        #endregion

        #region ATTDEF in a space and the ranges of an ELLIPSE

        [Fact]
        public void AnAttdefInModelSpace_IsMade()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            // AutoCAD 2021 (console, l2p pa, D1106): an ATTDEF in model space gives its list
            Assert.Equal(
                "((0 . \"ATTDEF\") (10 0.0 30.0 0.0) (40 . 2.5) (1 . \"D\") (3 . \"P\") (2 . \"TAGM\") (70 . 0))",
                Value(
                    session,
                    "(entmake '((0 . \"ATTDEF\") (10 0.0 30.0 0.0) (40 . 2.5) (1 . \"D\") (3 . \"P\") (2 . \"TAGM\") (70 . 0)))"
                )
            );

            Assert.Equal(1, document.EntityCount);
            Assert.Equal("\"ATTDEF\"", TypeOfLast(session));
            Assert.Equal("\"TAGM\"", Value(session, "(cdr (assoc 2 (entget (entlast))))"));
            Assert.Equal("\"P\"", Value(session, "(cdr (assoc 3 (entget (entlast))))"));
            Assert.Equal("\"D\"", Value(session, "(cdr (assoc 1 (entget (entlast))))"));
        }

        [Theory]
        [InlineData("(41 . 5.0) (42 . 8.0)")]
        [InlineData("(41 . 1.0) (42 . 20.0)")]
        [InlineData("(41 . -1.0) (42 . 1.0)")]
        public void AnEllipseWhoseParametersAreOutsideZeroToTwoPi_IsNil(string range)
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            // AutoCAD 2021 (console, l2p pc): all three are nil (the presenter made them before)
            Assert.Equal(
                "nil",
                Value(
                    session,
                    "(entmake '((0 . \"ELLIPSE\") (10 0.0 0.0 0.0) (11 5.0 0.0 0.0) (40 . 0.5) " + range + "))"
                )
            );
            Assert.Equal(0, document.EntityCount);
        }

        #endregion

        #region Records of the tables

        private const string LayerMarkers = "(100 . \"AcDbSymbolTableRecord\") (100 . \"AcDbLayerTableRecord\")";

        [Fact]
        public void ALayerRecordWithoutBothSubclassMarkers_GivesNil()
        {
            CadCommandSession session = Start();

            // AutoCAD 2021 (console, l2p pa)
            Assert.Equal(
                "nil",
                Value(session, "(entmake '((0 . \"LAYER\") (2 . \"GHL1\") (70 . 0) (62 . 3) (6 . \"Continuous\")))")
            );
            Assert.Equal("nil", Value(session, "(tblsearch \"LAYER\" \"GHL1\")"));
        }

        [Fact]
        public void ALayerRecord_IsMade_AndAnotherByTheSameNameIsNil()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            string made =
                "(entmake '((0 . \"LAYER\") "
                + LayerMarkers
                + " (2 . \"GHL2\") (70 . 0) (62 . 3) (6 . \"Continuous\")))";

            // AutoCAD 2021 (console, l2p pa): the list; a second GHL2 with colour 4 is nil; entlast does not change
            Assert.Equal(
                "((0 . \"LAYER\") " + LayerMarkers + " (2 . \"GHL2\") (70 . 0) (62 . 3) (6 . \"Continuous\"))",
                Value(session, made)
            );
            Assert.Equal(
                "nil",
                Value(
                    session,
                    "(entmake '((0 . \"LAYER\") "
                        + LayerMarkers
                        + " (2 . \"GHL2\") (70 . 0) (62 . 4) (6 . \"Continuous\")))"
                )
            );
            Assert.Equal("nil", Value(session, "(entlast)"));
            Assert.Equal(
                "((0 . \"LAYER\") (2 . \"GHL2\") (70 . 0) (62 . 3) (6 . \"Continuous\"))",
                Value(session, "(tblsearch \"LAYER\" \"GHL2\")")
            );
        }

        [Fact]
        public void ALayerRecordWithNoColourOrLinetype_IsColour7AndContinuous()
        {
            CadCommandSession session = Start();
            Value(session, "(entmake '((0 . \"LAYER\") " + LayerMarkers + " (2 . \"GHL3\") (70 . 0)))");

            // AutoCAD 2021 (console, l2p pa): (tblsearch "LAYER" "GHL3")
            Assert.Equal(
                "((0 . \"LAYER\") (2 . \"GHL3\") (70 . 0) (62 . 7) (6 . \"Continuous\"))",
                Value(session, "(tblsearch \"LAYER\" \"GHL3\")")
            );
        }

        [Fact]
        public void ALayerRecordNamingALinetypeThatIsNotLoaded_WritesTheError_AndGivesNil()
        {
            CadCommandSession session = Start();

            // AutoCAD 2021 (console, l2p pa): the error has no line break after it
            Assert.Equal(
                "Error: Undefined line type NOSUCH in LayerTableRecord GHL4nil",
                Value(
                    session,
                    "(entmake '((0 . \"LAYER\") " + LayerMarkers + " (2 . \"GHL4\") (70 . 0) (6 . \"NOSUCH\")))"
                )
            );
            Assert.Equal("nil", Value(session, "(tblsearch \"LAYER\" \"GHL4\")"));
        }

        [Fact]
        public void ALayerRecordWhoseNameIsNotValid_GivesNilSilently()
        {
            CadCommandSession session = Start();

            // AutoCAD 2021 (console, l2p pa)
            Assert.Equal(
                "nil",
                Value(session, "(entmake '((0 . \"LAYER\") " + LayerMarkers + " (2 . \"a*b\") (70 . 0)))")
            );
        }

        [Fact]
        public void ALayerRecordWithFlag5_IsFrozenAndLocked_AndKeepsItsOffColour()
        {
            CadCommandSession session = Start();
            Value(
                session,
                "(entmake '((0 . \"LAYER\") " + LayerMarkers + " (2 . \"GHL5\") (70 . 5) (62 . -2) (370 . 30)))"
            );

            // AutoCAD 2021 (console, l2p pa): 70 5 is frozen and locked, colour -2 is colour 2 turned off
            Assert.Equal(
                "((0 . \"LAYER\") (2 . \"GHL5\") (70 . 5) (62 . -2) (6 . \"Continuous\"))",
                Value(session, "(tblsearch \"LAYER\" \"GHL5\")")
            );
            Assert.Equal("30", Value(session, "(cdr (assoc 370 (entget (tblobjname \"LAYER\" \"GHL5\"))))"));
        }

        [Fact]
        public void ALinetypeRecord_IsMade_AndTblsearchListsNoGroup74WhereItIs0()
        {
            CadCommandSession session = Start();
            string list =
                "((0 . \"LTYPE\") (100 . \"AcDbSymbolTableRecord\") (100 . \"AcDbLinetypeTableRecord\") (2 . \"GHDASH\") (70 . 0) (3 . \"Dash\") (72 . 65) (73 . 2) (40 . 0.75) (49 . 0.5) (74 . 0) (49 . -0.25) (74 . 0))";

            // AutoCAD 2021 (console, l2p pa, D1110)
            Assert.Equal(list, Value(session, "(entmake '" + list + ")"));
            Assert.Equal(
                "((0 . \"LTYPE\") (2 . \"GHDASH\") (70 . 0) (3 . \"Dash\") (72 . 65) (73 . 2) (40 . 0.75) (49 . 0.5) (49 . -0.25))",
                Value(session, "(tblsearch \"LTYPE\" \"GHDASH\")")
            );
        }

        [Fact]
        public void ATextStyleRecord_IsMade_AndTblsearchGivesItsRecord()
        {
            CadCommandSession session = Start();
            string list =
                "((0 . \"STYLE\") (100 . \"AcDbSymbolTableRecord\") (100 . \"AcDbTextStyleTableRecord\") (2 . \"GHS\") (70 . 0) (40 . 0.0) (41 . 1.0) (50 . 0.0) (71 . 0) (42 . 2.5) (3 . \"txt\") (4 . \"\"))";

            // AutoCAD 2021 (console, l2p pa)
            Assert.Equal(list, Value(session, "(entmake '" + list + ")"));
            Assert.Equal(
                "((0 . \"STYLE\") (2 . \"GHS\") (70 . 0) (40 . 0.0) (41 . 1.0) (50 . 0.0) (71 . 0) (42 . 2.5) (3 . \"txt\") (4 . \"\"))",
                Value(session, "(tblsearch \"STYLE\" \"GHS\")")
            );
        }

        [Fact]
        public void TblsearchOfATextStyleGivesTheObliquingInRadians()
        {
            CadCommandSession session = Start();
            Value(
                session,
                "(entmake '((0 . \"STYLE\") (100 . \"AcDbSymbolTableRecord\") (100 . \"AcDbTextStyleTableRecord\") (2 . \"GHO\") (70 . 0) (40 . 0.0) (41 . 1.0) (50 . 0.2617993877991494) (71 . 0) (42 . 2.5) (3 . \"txt\") (4 . \"\")))"
            );

            // AutoCAD 2021 (console, l2p p3, D1109): the 50 of tblsearch STYLE is in radians, 0.261799 for 15 degrees
            Assert.Equal(
                "0.261799",
                Value(session, "(rtos (cdr (assoc 50 (tblsearch \"STYLE\" \"GHO\"))) 2 6)").Trim('"')
            );
        }

        [Fact]
        public void AnApplicationRecord_IsMade_AndTblsearchGivesIt()
        {
            CadCommandSession session = Start();
            string list =
                "((0 . \"APPID\") (100 . \"AcDbSymbolTableRecord\") (100 . \"AcDbRegAppTableRecord\") (2 . \"GHAPP\") (70 . 0))";

            // AutoCAD 2021 (console, l2p pa)
            Assert.Equal(list, Value(session, "(entmake '" + list + ")"));
            Assert.Equal(
                "((0 . \"APPID\") (2 . \"GHAPP\") (70 . 0))",
                Value(session, "(tblsearch \"APPID\" \"GHAPP\")")
            );
        }

        [Fact]
        public void ADimensionStyleRecord_IsMade_WithTheDrawingsDefaultsForEveryGroupNotGiven()
        {
            CadCommandSession session = Start();
            string list =
                "((0 . \"DIMSTYLE\") (100 . \"AcDbSymbolTableRecord\") (100 . \"AcDbDimStyleTableRecord\") (2 . \"GHD\") (70 . 0) (41 . 3.0))";

            // AutoCAD 2021 (console, l2p pa, D81): the groups not given take the drawing's imperial defaults
            Assert.Equal(list, Value(session, "(entmake '" + list + ")"));
            Assert.Equal(
                "((0 . \"DIMSTYLE\") (2 . \"GHD\") (70 . 0) (3 . \"\") (4 . \"\") (5 . \"\") (6 . \"\") (7 . \"\") (40 . 1.0) (41 . 3.0) (42 . 0.0625) (43 . 0.38) (44 . 0.18) (45 . 0.0) (46 . 0.0) (47 . 0.0) (48 . 0.0) (140 . 0.18) (141 . 0.09) (142 . 0.0) (143 . 25.4) (144 . 1.0) (145 . 0.0) (146 . 1.0) (147 . 0.09) (71 . 0) (72 . 0) (73 . 1) (74 . 1) (75 . 0) (76 . 0) (77 . 0) (78 . 0) (170 . 0) (171 . 2) (172 . 0) (173 . 0) (174 . 0) (175 . 0) (176 . 0) (177 . 0) (178 . 0) (270 . 2) (271 . 4) (272 . 4) (273 . 2) (274 . 2) (340 . <Entity name: *>) (275 . 0) (280 . 0) (281 . 0) (282 . 0) (283 . 1) (284 . 0) (285 . 0) (286 . 0) (287 . 3) (288 . 0))",
                Value(session, "(tblsearch \"DIMSTYLE\" \"GHD\")")
            );
        }

        [Fact]
        public void AUcsRecord_IsMade_AndTblsearchAdds79And146()
        {
            CadCommandSession session = Start();
            string list =
                "((0 . \"UCS\") (100 . \"AcDbSymbolTableRecord\") (100 . \"AcDbUCSTableRecord\") (2 . \"GHU\") (70 . 0) (10 1.0 2.0 0.0) (11 1.0 0.0 0.0) (12 0.0 1.0 0.0))";

            // AutoCAD 2021 (console, l2p pa)
            Assert.Equal(list, Value(session, "(entmake '" + list + ")"));
            Assert.Equal(
                "((0 . \"UCS\") (2 . \"GHU\") (70 . 0) (10 1.0 2.0 0.0) (11 1.0 0.0 0.0) (12 0.0 1.0 0.0) (79 . 0) (146 . 0.0))",
                Value(session, "(tblsearch \"UCS\" \"GHU\")")
            );
        }

        [Fact]
        public void AViewRecord_IsMade_AndTblsearchAddsItsDefaults()
        {
            CadCommandSession session = Start();
            string list =
                "((0 . \"VIEW\") (100 . \"AcDbSymbolTableRecord\") (100 . \"AcDbViewTableRecord\") (2 . \"GHV\") (70 . 0) (40 . 100.0) (10 0.0 0.0) (41 . 200.0) (11 0.0 0.0 1.0) (12 0.0 0.0 0.0) (42 . 50.0) (43 . 0.0) (44 . 0.0) (50 . 0.0) (71 . 0))";

            // AutoCAD 2021 (console, l2p pa)
            Assert.Equal(list, Value(session, "(entmake '" + list + ")"));
            Assert.Equal(
                "((0 . \"VIEW\") (2 . \"GHV\") (70 . 0) (40 . 100.0) (10 0.0 0.0) (41 . 200.0) (11 0.0 0.0 1.0) (12 0.0 0.0 0.0) (42 . 50.0) (43 . 0.0) (44 . 0.0) (50 . 0.0) (71 . 0) (72 . 0) (110 0.0 0.0 0.0) (111 1.0 0.0 0.0) (112 0.0 1.0 0.0) (79 . 0) (146 . 0.0))",
                Value(session, "(tblsearch \"VIEW\" \"GHV\")")
            );
        }

        [Fact]
        public void AViewportRecord_IsMade()
        {
            CadCommandSession session = Start();
            string list =
                "((0 . \"VPORT\") (100 . \"AcDbSymbolTableRecord\") (100 . \"AcDbViewportTableRecord\") (2 . \"GHVP\") (70 . 0))";

            // AutoCAD 2021 (console, l2p pa): the list; what tblsearch gives was not probed
            Assert.Equal(list, Value(session, "(entmake '" + list + ")"));
            Assert.NotEqual("nil", Value(session, "(tblsearch \"VPORT\" \"GHVP\")"));
        }

        [Fact]
        public void ABlockRecord_IsNotMadeByEntmake()
        {
            CadCommandSession session = Start();

            // AutoCAD 2021 (console, l2p pa): BLOCK_RECORD gives nil, and entlast does not change
            Assert.Equal("nil", Value(session, "(entmake '((0 . \"BLOCK_RECORD\") (2 . \"GHBR\")))"));
            Assert.Equal("nil", Value(session, "(tblsearch \"BLOCK\" \"GHBR\")"));
        }

        [Fact]
        public void ATableRecordMadeByEntmake_IsOneUndoStepAndOneUTakesItBack()
        {
            CadCommandSession session = Start();
            Value(
                session,
                "(entmake '((0 . \"LAYER\") "
                    + LayerMarkers
                    + " (2 . \"GHL2\") (70 . 0) (62 . 3) (6 . \"Continuous\")))"
            );
            Assert.NotEqual("nil", Value(session, "(tblsearch \"LAYER\" \"GHL2\")"));

            // D1104: the change joins the typed form's undo step (D42)
            Run(session, "U");

            Assert.Equal("nil", Value(session, "(tblsearch \"LAYER\" \"GHL2\")"));
        }

        #endregion
    }
}
