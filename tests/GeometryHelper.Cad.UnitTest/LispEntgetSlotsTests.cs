using System;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;
using static GeometryHelper.Cad.UnitTest.LispEntityTests;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// <c>entget</c> of the entities that came with no slots of their own: an ATTRIB, an ATTDEF, a SEQEND, an MTEXT, a POLYLINE
    /// and its VERTEXes, and the BLOCK that opens a block definition, with the lists AutoCAD 2021's accoreconsole gave for the same
    /// entities (L2 parked probes pa and pd, made there by entmake), and the angles of a HATCH in radians (probe pc). The entities
    /// are read from a DXF the test writes or made by the library, as entmake of a complex entity is another part's. The presenter
    /// keeps no linetype of its own in a record that has none, so the <c>(6 . "Continuous")</c> AutoCAD's lists hold after the
    /// layer is not here; an entity name and a handle are written <c>*</c>, as the harness normalises them.
    /// </summary>
    public sealed class LispEntgetSlotsTests : IDisposable
    {
        // Ends the routine a test left waiting, so no worker thread outlives its test.
        public void Dispose() => WaitingRoutines.EndAll();

        private static CadCommandSession Drawn(params string[] entities) =>
            Session(CadDocument.From(TestDrawings.Drawing(null, null, entities), "slots.dxf"));

        // The groups of a list as the harness writes them, up to the 410 of an entity of model space.
        private static string Row(string type, string rest) =>
            "((-1 . <Entity name: *>) (0 . \""
            + type
            + "\") (330 . <Entity name: *>) (5 . \"*\") (100 . \"AcDbEntity\") (67 . 0) "
            + rest
            + ")";

        private static string Marker(bool on, string name) => on ? "100\n" + name + "\n" : string.Empty;

        // A block reference of GHB at (20,0) with an attribute TAG1 reading V1 at (20,2), the way AutoCAD 2000's DXF writes them.
        private static CadCommandSession WithAnAttribute()
        {
            CadCommandSession session = Drawn(
                "0\nINSERT\n5\nB1\n8\n0\n66\n1\n2\nGHB\n10\n20\n20\n0\n30\n0\n"
                    + "0\nATTRIB\n5\nB2\n8\n0\n10\n20\n20\n2\n30\n0\n40\n2.5\n1\nV1\n2\nTAG1\n70\n0\n"
                    + "0\nSEQEND\n5\nB3\n8\n0\n"
            );
            Value(session, "(setq i (entlast))");
            Value(session, "(setq a (entnext i))");
            Value(session, "(setq s (entnext a))");
            return session;
        }

        // A block GHB of a line from (0,0) to (10,0) and an attribute definition TAG1, in a drawing with its table of blocks.
        private static CadCommandSession WithABlock()
        {
            CadDocument document = Blank();
            var block = new DxfBlockTableRecord { Name = "GHB" };
            document.Database.BlockTable.Add(block);
            block.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0)));
            block.AppendEntity(
                new DxfAttributeDefinition(new GeoPoint3(0, 2, 0), "D", "TAG1", "Prompt", (DxfTextStyleTableRecord)null)
            );
            return Session(document);
        }

        [Fact]
        public void EntgetOfAnAttribute_GivesTheGroupsOfAutoCadsListInItsOrder()
        {
            CadCommandSession session = WithAnAttribute();

            // AutoCAD 2021 (console, l2p pa): ... (100 . "AcDbText") (10 20.0 2.0 0.0) (40 . 2.5) (1 . "V1") (50 . 0.0) (41 . 1.0)
            // (51 . 0.0) (7 . "Standard") (71 . 0) (72 . 0) (11 0.0 0.0 0.0) (210 0.0 0.0 1.0) (100 . "AcDbAttribute") (280 . 0)
            // (2 . "TAG1") (70 . 0) (73 . 0) (74 . 0) (280 . 0)
            Assert.Equal(
                Row(
                    "ATTRIB",
                    "(410 . \"Model\") (8 . \"0\") (100 . \"AcDbText\") (10 20.0 2.0 0.0) (40 . 2.5) (1 . \"V1\") (50 . 0.0) (41 . 1.0) (51 . 0.0) (7 . \"Standard\") (71 . 0) (72 . 0) (11 0.0 0.0 0.0) (210 0.0 0.0 1.0) (100 . \"AcDbAttribute\") (280 . 0) (2 . \"TAG1\") (70 . 0) (73 . 0) (74 . 0) (280 . 0)"
                ),
                Value(session, "(entget a)")
            );
            Assert.Equal("T", Value(session, "(equal (cdr (assoc 330 (entget a))) i)"));
        }

        [Fact]
        public void AnAttributeThatHasBothGroups280_KeepsTheVersionFirstAndTheLockLast()
        {
            CadCommandSession session = Drawn(
                "0\nINSERT\n5\nB1\n8\n0\n66\n1\n2\nGHB\n10\n20\n20\n0\n30\n0\n"
                    + "0\nATTRIB\n5\nB2\n100\nAcDbEntity\n8\n0\n100\nAcDbText\n10\n20\n20\n2\n30\n0\n40\n2.5\n1\nV1\n"
                    + "100\nAcDbAttribute\n280\n0\n2\nTAG1\n70\n0\n280\n1\n"
                    + "0\nSEQEND\n5\nB3\n8\n0\n"
            );
            Value(session, "(setq a (entnext (entlast)))");

            // the lock of the position (the 280 after the tag) is 1 here, and it stays the last group, as AutoCAD writes it
            Assert.Equal(
                "((100 . \"AcDbAttribute\") (280 . 0) (2 . \"TAG1\") (70 . 0) (73 . 0) (74 . 0) (280 . 1))",
                Value(session, "(member '(100 . \"AcDbAttribute\") (entget a))")
            );
        }

        [Fact]
        public void AnAttributeWithOnlyTheLockInItsRecord_HasTheVersionAdded()
        {
            CadCommandSession session = Drawn(
                "0\nINSERT\n5\nB1\n8\n0\n66\n1\n2\nGHB\n10\n20\n20\n0\n30\n0\n"
                    + "0\nATTRIB\n5\nB2\n8\n0\n10\n20\n20\n2\n30\n0\n40\n2.5\n1\nV1\n2\nTAG1\n70\n0\n280\n1\n"
                    + "0\nSEQEND\n5\nB3\n8\n0\n"
            );
            Value(session, "(setq a (entnext (entlast)))");

            // ours: a 280 after the tag is the lock, whatever the file has before it
            Assert.Equal(
                "((100 . \"AcDbAttribute\") (280 . 0) (2 . \"TAG1\") (70 . 0) (73 . 0) (74 . 0) (280 . 1))",
                Value(session, "(member '(100 . \"AcDbAttribute\") (entget a))")
            );
        }

        [Fact]
        public void EntgetOfAnAttributeDefinition_OfABlock_HasNoSpaceName()
        {
            CadCommandSession session = WithABlock();

            // AutoCAD 2021 (console, l2p pa): the ATTDEF of a block made by entmake: 67, 8, 6 and no 410; then the same as an
            // ATTRIB's with (100 . "AcDbAttributeDefinition") (280 . 0) (3 . "Prompt") (2 . "TAG1") (70 . 0) (73 . 0) (74 . 0) (280 . 0)
            Assert.Equal(
                Row(
                    "ATTDEF",
                    "(8 . \"0\") (100 . \"AcDbText\") (10 0.0 2.0 0.0) (40 . 2.5) (1 . \"D\") (50 . 0.0) (41 . 1.0) (51 . 0.0) (7 . \"Standard\") (71 . 0) (72 . 0) (11 0.0 0.0 0.0) (210 0.0 0.0 1.0) (100 . \"AcDbAttributeDefinition\") (280 . 0) (3 . \"Prompt\") (2 . \"TAG1\") (70 . 0) (73 . 0) (74 . 0) (280 . 0)"
                ),
                Value(session, "(entget (entnext (cdr (assoc -2 (tblsearch \"BLOCK\" \"GHB\")))))")
            );
        }

        [Fact]
        public void EntgetOfAnAttributeDefinition_OfModelSpace_HasTheSpaceName()
        {
            CadCommandSession session = Drawn(
                "0\nATTDEF\n5\nA1\n8\n0\n10\n0\n20\n30\n30\n0\n40\n2.5\n1\nD\n3\nP\n2\nTAGM\n70\n0\n"
            );

            // AutoCAD 2021 (console, l2p pa): an ATTDEF made in model space: (410 . "Model") (8 . "0") ... (3 . "P") (2 . "TAGM")
            Assert.Equal(
                Row(
                    "ATTDEF",
                    "(410 . \"Model\") (8 . \"0\") (100 . \"AcDbText\") (10 0.0 30.0 0.0) (40 . 2.5) (1 . \"D\") (50 . 0.0) (41 . 1.0) (51 . 0.0) (7 . \"Standard\") (71 . 0) (72 . 0) (11 0.0 0.0 0.0) (210 0.0 0.0 1.0) (100 . \"AcDbAttributeDefinition\") (280 . 0) (3 . \"P\") (2 . \"TAGM\") (70 . 0) (73 . 0) (74 . 0) (280 . 0)"
                ),
                Value(session, "(entget (entlast))")
            );
        }

        [Fact]
        public void EntgetOfASeqend_EndsWithTheEntityItEnds()
        {
            CadCommandSession session = WithAnAttribute();

            // AutoCAD 2021 (console, l2p pa): (330 . owner) (100 . "AcDbEntity") (67 . 0) (410 . "Model") (8 . "0") (6 . "Continuous")
            // (-2 . <the INSERT>)
            Assert.Equal(
                Row("SEQEND", "(410 . \"Model\") (8 . \"0\") (-2 . <Entity name: *>)"),
                Value(session, "(entget s)")
            );
            Assert.Equal("T", Value(session, "(equal (cdr (assoc -2 (entget s))) i)"));
        }

        private static CadCommandSession WithAnMText(string contents)
        {
            CadDocument document = Blank();
            document.ApplyEdit("T", null, s =>
                s.Append(document.ModelSpace, new DxfMText { Location = new GeoPoint3(0, 40, 0), Contents = contents })
            );
            return Session(document);
        }

        [Fact]
        public void EntgetOfAnMText_GivesTheGroupsOfAutoCadsListInItsOrder()
        {
            CadCommandSession session = WithAnMText("mtext body");
            Value(session, "(setq e (entget (entlast)))");

            // AutoCAD 2021 (console, l2p pa): (100 . "AcDbMText") (10 0.0 40.0 0.0) (40 . 2.5) (41 . 0.0) (46 . 0.0) (71 . 1)
            // (72 . 5) (1 . "mtext body") (7 . "Standard") (210 0.0 0.0 1.0) (11 1.0 0.0 0.0) (42 . 24.1667) (43 . 3.33333)
            // (50 . 0.0) (73 . 1) (44 . 1.0); 42 and 43 are the extents of the text in its font, which the presenter measures with
            // its own where the drawing's font search path finds one (the test has none, so they read 0.0), so they are asked to be numbers
            Assert.Equal(
                "(-1 0 330 5 100 67 410 8 100 10 40 41 46 71 72 1 7 210 11 42 43 50 73 44)",
                Value(session, "(mapcar 'car e)")
            );
            Assert.Equal(
                "((10 0.0 40.0 0.0) (40 . 2.5) (41 . 0.0) (46 . 0.0) (71 . 1) (72 . 5) (1 . \"mtext body\") (7 . \"Standard\") (210 0.0 0.0 1.0) (11 1.0 0.0 0.0) (50 . 0.0) (73 . 1) (44 . 1.0))",
                Value(session, "(mapcar '(lambda (c) (assoc c e)) '(10 40 41 46 71 72 1 7 210 11 50 73 44))")
            );
            Assert.Equal("T", Value(session, "(and (numberp (cdr (assoc 42 e))) (numberp (cdr (assoc 43 e))))"));
        }

        [Fact]
        public void AnMTextOfSeveralChunks_KeepsItsChunksBeforeTheLastOne()
        {
            CadCommandSession session = Drawn(
                "0\nMTEXT\n5\n2A\n100\nAcDbEntity\n8\n0\n100\nAcDbMText\n10\n0\n20\n0\n30\n0\n40\n2.5\n71\n1\n"
                    + "3\nfirst \n3\nsecond \n1\nlast\n"
            );

            // the chunks of the text, group 3, come in their order before the group 1 that ends it, as a file and AutoCAD give them
            Assert.Equal(
                "((3 . \"first \") (3 . \"second \") (1 . \"last\"))",
                Value(session, "(vl-remove-if-not '(lambda (g) (member (car g) '(1 3))) (entget (entlast)))")
            );
        }

        private const string TwoD =
            "(100 . \"AcDb2dPolyline\") (66 . 1) (10 0.0 0.0 0.0) (70 . 0) (40 . 0.0) (41 . 0.0) (210 0.0 0.0 1.0) (71 . 0) (72 . 0) (73 . 0) (74 . 0) (75 . 0)";

        private static CadCommandSession WithAPolyline(bool markers)
        {
            string polyline =
                "0\nPOLYLINE\n5\n1A\n"
                + Marker(markers, "AcDbEntity")
                + "8\n0\n"
                + Marker(markers, "AcDb2dPolyline")
                + "66\n1\n10\n0\n20\n0\n30\n0\n70\n0\n";
            string vertex =
                Marker(markers, "AcDbEntity")
                + "8\n0\n"
                + Marker(markers, "AcDbVertex")
                + Marker(markers, "AcDb2dVertex");
            CadCommandSession session = Drawn(
                polyline
                    + "0\nVERTEX\n5\n1B\n"
                    + vertex
                    + "10\n0\n20\n0\n30\n0\n"
                    + "0\nVERTEX\n5\n1C\n"
                    + vertex
                    + "10\n10\n20\n0\n30\n0\n42\n0.5\n"
                    + "0\nSEQEND\n5\n1D\n8\n0\n"
            );
            Value(session, "(setq p (entlast))");
            Value(session, "(setq v1 (entnext p))");
            Value(session, "(setq v2 (entnext v1))");
            Value(session, "(setq s (entnext v2))");
            return session;
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void EntgetOfATwoDimensionalPolyline_GivesTheGroupsOfAutoCadsList(bool markersInTheFile)
        {
            CadCommandSession session = WithAPolyline(markersInTheFile);

            // AutoCAD 2021 (console, l2p pa): (100 . "AcDb2dPolyline") (66 . 1) (10 0.0 0.0 0.0) (70 . 0) (40 . 0.0) (41 . 0.0)
            // (210 0.0 0.0 1.0) (71 . 0) (72 . 0) (73 . 0) (74 . 0) (75 . 0)
            Assert.Equal(Row("POLYLINE", "(410 . \"Model\") (8 . \"0\") " + TwoD), Value(session, "(entget p)"));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void EntgetOfAVertexOfATwoDimensionalPolyline_GivesTheGroupsOfAutoCadsList(bool markersInTheFile)
        {
            CadCommandSession session = WithAPolyline(markersInTheFile);

            // AutoCAD 2021 (console, l2p pa): (100 . "AcDbVertex") (100 . "AcDb2dVertex") (10 10.0 0.0 0.0) (40 . 0.0) (41 . 0.0)
            // (42 . 0.5) (91 . 0) (70 . 0) (50 . 0.0)
            Assert.Equal(
                Row(
                    "VERTEX",
                    "(410 . \"Model\") (8 . \"0\") (100 . \"AcDbVertex\") (100 . \"AcDb2dVertex\") (10 10.0 0.0 0.0) (40 . 0.0) (41 . 0.0) (42 . 0.5) (91 . 0) (70 . 0) (50 . 0.0)"
                ),
                Value(session, "(entget v2)")
            );
            Assert.Equal(
                Row(
                    "VERTEX",
                    "(410 . \"Model\") (8 . \"0\") (100 . \"AcDbVertex\") (100 . \"AcDb2dVertex\") (10 0.0 0.0 0.0) (40 . 0.0) (41 . 0.0) (42 . 0.0) (91 . 0) (70 . 0) (50 . 0.0)"
                ),
                Value(session, "(entget v1)")
            );
            Assert.Equal("T", Value(session, "(equal (cdr (assoc 330 (entget v2))) p)"));
        }

        [Fact]
        public void TheSeqendOfAPolyline_HasThePolylineAsItsOwnerAndAsGroupMinusTwo()
        {
            CadCommandSession session = WithAPolyline(true);

            Assert.Equal(
                Row("SEQEND", "(410 . \"Model\") (8 . \"0\") (-2 . <Entity name: *>)"),
                Value(session, "(entget s)")
            );
            Assert.Equal("T", Value(session, "(equal (cdr (assoc -2 (entget s))) p)"));
            Assert.Equal("T", Value(session, "(equal (cdr (assoc 330 (entget s))) p)"));
        }

        [Fact]
        public void EntgetOfAThreeDimensionalPolyline_AndItsVertices_GivesTheGroupsOfAutoCadsList()
        {
            CadCommandSession session = Drawn(
                "0\nPOLYLINE\n5\n1A\n8\n0\n66\n1\n10\n0\n20\n0\n30\n0\n70\n9\n"
                    + "0\nVERTEX\n5\n1B\n8\n0\n10\n10\n20\n0\n30\n5\n70\n32\n"
                    + "0\nSEQEND\n5\n1D\n8\n0\n"
            );
            Value(session, "(setq p (entlast))");

            // AutoCAD 2021 (console, l2p pa): (100 . "AcDb3dPolyline") (66 . 1) (10 0.0 0.0 0.0) (70 . 9) (40 . 0.0) (41 . 0.0)
            // (210 0.0 0.0 1.0) (71 . 0) (72 . 0) (73 . 0) (74 . 0) (75 . 0); the vertex (100 . "AcDbVertex")
            // (100 . "AcDb3dPolylineVertex") (10 10.0 0.0 5.0) (40 . 0.0) (41 . 0.0) (42 . 0.0) (70 . 32) (50 . 0.0) (71 . 0) (72 . 0)
            // (73 . 0) (74 . 0)
            Assert.Equal(
                Row(
                    "POLYLINE",
                    "(410 . \"Model\") (8 . \"0\") (100 . \"AcDb3dPolyline\") (66 . 1) (10 0.0 0.0 0.0) (70 . 9) (40 . 0.0) (41 . 0.0) (210 0.0 0.0 1.0) (71 . 0) (72 . 0) (73 . 0) (74 . 0) (75 . 0)"
                ),
                Value(session, "(entget p)")
            );
            Assert.Equal(
                Row(
                    "VERTEX",
                    "(410 . \"Model\") (8 . \"0\") (100 . \"AcDbVertex\") (100 . \"AcDb3dPolylineVertex\") (10 10.0 0.0 5.0) (40 . 0.0) (41 . 0.0) (42 . 0.0) (70 . 32) (50 . 0.0) (71 . 0) (72 . 0) (73 . 0) (74 . 0)"
                ),
                Value(session, "(entget (entnext p))")
            );
        }

        [Fact]
        public void EntgetOfTheBlockThatOpensADefinition_GivesTheGroupsOfAutoCadsList()
        {
            CadCommandSession session = WithABlock();

            // AutoCAD 2021 (console, l2p pa): (entget (tblobjname "BLOCK" "GHE1")): (330 . owner) (100 . "AcDbEntity") (67 . 0)
            // (8 . "0") (100 . "AcDbBlockBegin") (70 . 2) (10 0.0 0.0 0.0) (-2 . <first entity>) (2 . "GHE1") (1 . "GHE1"); the
            // path of a block made by entmake is its name, here the empty path of a block made in code (ours, not measured)
            Assert.Equal(
                "((-1 . <Entity name: *>) (0 . \"BLOCK\") (330 . <Entity name: *>) (5 . \"*\") (100 . \"AcDbEntity\") (67 . 0) (8 . \"0\") (100 . \"AcDbBlockBegin\") (70 . 2) (10 0.0 0.0 0.0) (-2 . <Entity name: *>) (2 . \"GHB\") (1 . \"\"))",
                Value(session, "(entget (tblobjname \"BLOCK\" \"GHB\"))")
            );
            Assert.Equal(
                "T",
                Value(
                    session,
                    "(equal (cdr (assoc -2 (entget (tblobjname \"BLOCK\" \"GHB\")))) (cdr (assoc -2 (tblsearch \"BLOCK\" \"GHB\"))))"
                )
            );
        }

        [Fact]
        public void TheBlockEntityIsNotTheRecord_ButStandsForTheBlockInTheObjectModel()
        {
            CadCommandSession session = WithABlock();
            Value(session, "(setq first (cdr (assoc -2 (tblsearch \"BLOCK\" \"GHB\"))))");

            // the BLOCK entity and the BLOCK_RECORD are two objects, as in AutoCAD: the owner of the line is the record
            Assert.Equal(
                "nil",
                Value(session, "(equal (tblobjname \"BLOCK\" \"GHB\") (cdr (assoc 330 (entget first))))")
            );
            Assert.Equal(
                "\"BLOCK_RECORD\"",
                Value(session, "(cdr (assoc 0 (entget (cdr (assoc 330 (entget first))))))")
            );
            Value(session, "(vl-load-com)");
            Assert.Equal(
                "\"GHB\"",
                Value(session, "(vla-get-name (vlax-ename->vla-object (tblobjname \"BLOCK\" \"GHB\")))")
            );
        }

        [Fact]
        public void AnEntityOfABlockDefinition_HasNoSpaceName()
        {
            CadCommandSession session = WithABlock();

            // AutoCAD 2021 (console, l2p pa): the entities of a block definition list 67, 8, 6 and no 410
            Assert.Equal(
                "((-1 . <Entity name: *>) (0 . \"LINE\") (330 . <Entity name: *>) (5 . \"*\") (100 . \"AcDbEntity\") (67 . 0) (8 . \"0\") (100 . \"AcDbLine\") (10 0.0 0.0 0.0) (11 10.0 0.0 0.0) (210 0.0 0.0 1.0))",
                Value(session, "(entget (cdr (assoc -2 (tblsearch \"BLOCK\" \"GHB\"))))")
            );
        }

        // A hatch of the pattern ANSI31 at 45 degrees whose boundary is one arc from 270 to 450 degrees (DXF writes degrees).
        private static CadCommandSession WithAHatch() =>
            Drawn(
                "0\nHATCH\n5\n3A\n100\nAcDbEntity\n8\n0\n100\nAcDbHatch\n10\n0\n20\n0\n30\n0\n210\n0\n220\n0\n230\n1\n"
                    + "2\nANSI31\n70\n0\n71\n0\n91\n1\n92\n1\n93\n1\n72\n2\n10\n5\n20\n5\n40\n3\n50\n270\n51\n450\n73\n1\n97\n0\n"
                    + "75\n1\n76\n1\n52\n0\n41\n1\n77\n0\n78\n1\n53\n45\n43\n0\n44\n0\n45\n-1.4142135623730951\n"
                    + "46\n1.4142135623730951\n79\n0\n98\n0\n"
            );

        [Fact]
        public void EntgetOfAHatch_GivesItsPatternAngleAndTheAnglesOfItsArcEdgesInRadians()
        {
            CadCommandSession session = WithAHatch();
            Value(session, "(setq e (entget (entlast)))");

            // AutoCAD 2021 (console, l2p pc): ANSI31 gives (53 . 0.785398) and (52 . 0.0); an arc edge from 270 to 450 degrees
            // gives (50 . 4.71239) (51 . 7.85398)
            Assert.Equal("0.785398", Value(session, "(cdr (assoc 53 e))"));
            Assert.Equal("0.0", Value(session, "(cdr (assoc 52 e))"));
            Assert.Equal("4.71239", Value(session, "(cdr (assoc 50 e))"));
            Assert.Equal("7.85398", Value(session, "(cdr (assoc 51 e))"));
        }

        [Fact]
        public void EntmodOfAHatchGivenBackItsOwnList_TurnsTheRadiansBackIntoTheDegreesOfTheFile()
        {
            CadCommandSession session = WithAHatch();
            Value(session, "(setq e (entget (entlast)))");
            Value(session, "(entmod e)");

            // the angles read back the same after the round trip through the file's degrees
            Assert.Equal("0.785398", Value(session, "(cdr (assoc 53 (entget (entlast))))"));
            Assert.Equal("4.71239", Value(session, "(cdr (assoc 50 (entget (entlast))))"));
            Assert.Equal("7.85398", Value(session, "(cdr (assoc 51 (entget (entlast))))"));
        }
    }
}
