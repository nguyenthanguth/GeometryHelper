using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The leftovers of the L2 parked line that round 1 measured (second round, scratch of l2p2): the group 1 of the BLOCK entity
    /// of an entmade block, the ENDBLK an empty block names as its first entity, the linetype and colour of an entmade ATTRIB and
    /// of a SEQEND, an MTEXT's extents, the order of a hatch's loops and the tail of a layer's list. The expected values are those
    /// of AutoCAD 2021's accoreconsole in probes pf, pg, ph and pi of scratch/l2p2/probes, marked <c>AutoCAD 2021 (console, l2p2
    /// pf)</c> and so on; what the probes did not measure is marked <c>ours</c>. An entity name and a handle are written
    /// <c>*</c>, as the harness of the routines normalises them.
    /// </summary>
    public sealed class L2ParkedLeftoverTests : IDisposable
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

        // The block GHF1 of probe pf: a line in it, made by entmake.
        private static void MakeBlock(CadCommandSession session, string name, bool withLine)
        {
            Value(session, "(entmake '((0 . \"BLOCK\") (2 . \"" + name + "\") (70 . 0) (10 0.0 0.0 0.0)))");

            if (withLine)
            {
                Value(session, "(entmake '((0 . \"LINE\") (10 0.0 0.0 0.0) (11 10.0 0.0 0.0)))");
            }

            Value(session, "(entmake '((0 . \"ENDBLK\")))");
        }

        #region The BLOCK entity and the ENDBLK

        [Fact]
        public void ABlockMadeByEntmake_ListsItsNameAsTheGroup1OfItsBlockEntity()
        {
            CadCommandSession session = Start();
            MakeBlock(session, "GHF1", true);

            // AutoCAD 2021 (console, l2p2 pf): (entget (tblobjname "BLOCK" "GHF1")) ends (2 . "GHF1") (1 . "GHF1")
            Assert.Equal(
                "((-1 . <Entity name: *>) (0 . \"BLOCK\") (330 . <Entity name: *>) (5 . \"*\") (100 . \"AcDbEntity\") (67 . 0) (8 . \"0\") (100 . \"AcDbBlockBegin\") (70 . 0) (10 0.0 0.0 0.0) (-2 . <Entity name: *>) (2 . \"GHF1\") (1 . \"GHF1\"))",
                Value(session, "(entget (tblobjname \"BLOCK\" \"GHF1\"))")
            );
        }

        [Fact]
        public void AGroup1GivenToABlockOfEntmake_IsLeftOutAndItsNameListed()
        {
            CadCommandSession session = Start();
            Value(
                session,
                "(entmake '((0 . \"BLOCK\") (2 . \"GHF2\") (70 . 0) (10 0.0 0.0 0.0) (1 . \"given path\")))"
            );
            Value(session, "(entmake '((0 . \"ENDBLK\")))");

            // AutoCAD 2021 (console, l2p2 pf): the (1 . "given path") of the list is not kept, (1 . "GHF2") is listed
            Assert.Equal("\"GHF2\"", Value(session, "(cdr (assoc 1 (entget (tblobjname \"BLOCK\" \"GHF2\"))))"));
        }

        [Fact]
        public void AnAnonymousBlockOfEntmake_ListsTheNameItWasGivenAsItsGroup1()
        {
            CadCommandSession session = Start();
            Value(session, "(entmake '((0 . \"BLOCK\") (2 . \"*U\") (70 . 1) (10 0.0 0.0 0.0)))");
            Value(session, "(entmake '((0 . \"LINE\") (10 0.0 0.0 0.0) (11 1.0 0.0 0.0)))");
            Value(session, "(setq an (entmake '((0 . \"ENDBLK\"))))");

            // AutoCAD 2021 (console, l2p2 pf): the block is *U5 there (the next free number) and its group 1 is "*U"
            Assert.Equal("\"*U\"", Value(session, "(cdr (assoc 1 (entget (tblobjname \"BLOCK\" an))))"));
        }

        [Fact]
        public void ABlockMadeByTheBlockCommand_ListsAnEmptyGroup1()
        {
            CadCommandSession session = Start();
            Value(session, "(command \"LINE\" \"0,0\" \"5,0\" \"\")");
            Value(session, "(command \"-BLOCK\" \"GHF5\" \"0,0\" \"L\" \"\")");

            // AutoCAD 2021 (console, l2p2 pf): (2 . "GHF5") (1 . "")
            Assert.Equal("\"\"", Value(session, "(cdr (assoc 1 (entget (tblobjname \"BLOCK\" \"GHF5\"))))"));
        }

        [Fact]
        public void AnEmptyBlock_NamesItsEndblkAsItsFirstEntityInTheBlockEntityAndInTblsearch()
        {
            CadCommandSession session = Start();
            MakeBlock(session, "GHF4", false);

            // AutoCAD 2021 (console, l2p2 pf): tblsearch ends (10 0.0 0.0 0.0) (-2 . <ename>), the BLOCK entity's list has the same
            // -2 before (2 . "GHF4"), and (entget it) is the ENDBLK
            Assert.Equal(
                "((0 . \"BLOCK\") (2 . \"GHF4\") (70 . 0) (4 . \"\") (10 0.0 0.0 0.0) (-2 . <Entity name: *>))",
                Value(session, "(tblsearch \"BLOCK\" \"GHF4\")")
            );
            Assert.Equal(
                "T",
                Value(
                    session,
                    "(equal (cdr (assoc -2 (tblsearch \"BLOCK\" \"GHF4\"))) (cdr (assoc -2 (entget (tblobjname \"BLOCK\" \"GHF4\")))))"
                )
            );
            Assert.Equal(
                "((-1 . <Entity name: *>) (0 . \"ENDBLK\") (330 . <Entity name: *>) (5 . \"*\") (100 . \"AcDbEntity\") (67 . 0) (8 . \"0\") (100 . \"AcDbBlockEnd\"))",
                Value(session, "(entget (cdr (assoc -2 (tblsearch \"BLOCK\" \"GHF4\"))))")
            );
        }

        [Fact]
        public void TheEndblkOfAnEmptyBlock_IsNoEntityOfTheBlockToWalkTo()
        {
            CadCommandSession session = Start();
            MakeBlock(session, "GHF4", false);

            // AutoCAD 2021 (console, l2p2 pf): (entnext (tblobjname "BLOCK" "GHF4")) is nil, not the ENDBLK, and (entnext endblk) is nil
            Assert.Equal("nil", Value(session, "(entnext (tblobjname \"BLOCK\" \"GHF4\"))"));
            Assert.Equal("nil", Value(session, "(entnext (cdr (assoc -2 (tblsearch \"BLOCK\" \"GHF4\"))))"));
        }

        [Fact]
        public void TheBlockEntity_GoesOnToTheFirstEntityOfItsBlock()
        {
            CadCommandSession session = Start();
            MakeBlock(session, "GHF1", true);
            Value(session, "(command \"LINE\" \"0,0\" \"5,0\" \"\")");
            Value(session, "(command \"-BLOCK\" \"GHF5\" \"0,0\" \"L\" \"\")");

            // AutoCAD 2021 (console, l2p2 pf): the chain from the BLOCK entity of GHF1 and of GHF5 is the BLOCK and its LINE, and
            // nothing after the line (the ENDBLK is not walked to)
            Assert.Equal(
                "T",
                Value(
                    session,
                    "(equal (entnext (tblobjname \"BLOCK\" \"GHF1\")) (cdr (assoc -2 (entget (tblobjname \"BLOCK\" \"GHF1\")))))"
                )
            );
            Assert.Equal(
                "\"LINE\"",
                Value(session, "(cdr (assoc 0 (entget (entnext (tblobjname \"BLOCK\" \"GHF5\")))))")
            );
            Assert.Equal("nil", Value(session, "(entnext (entnext (tblobjname \"BLOCK\" \"GHF5\")))"));
        }

        [Fact]
        public void AnEmptyBlockReadFromADxf_NamesItsEndblkToo()
        {
            // The drawing is saved and read again, so that the block is one of a file as it was read.
            CadDocument made = Blank();
            made.Database.BlockTable.Add(new DxfBlockTableRecord { Name = "EMPTY" });
            string text;

            using (var stream = new MemoryStream())
            {
                made.Database.Save(stream);
                text = Encoding.UTF8.GetString(stream.ToArray());
            }

            CadCommandSession session = Session(CadDocument.From(DxfDatabase.Parse(text), "empty.dxf"));

            // ours: a block of a file as it was read has its ENDBLK in the file, named as an added block's is
            Assert.Equal(
                "\"ENDBLK\"",
                Value(session, "(cdr (assoc 0 (entget (cdr (assoc -2 (tblsearch \"BLOCK\" \"EMPTY\"))))))")
            );
        }

        #endregion

        #region The linetype and colour of an ATTRIB and a SEQEND

        // The block GHA of probe pf: one attribute definition, and a dashed linetype GHDASH.
        private static CadCommandSession WithAttributeBlock()
        {
            CadCommandSession session = Start();

            // the blank drawing of the harness draws in Continuous (CELTYPE), the library's new one by layer
            Value(session, "(setvar \"CELTYPE\" \"Continuous\")");
            Value(session, "(entmake '((0 . \"BLOCK\") (2 . \"GHA\") (70 . 2) (10 0.0 0.0 0.0)))");
            Value(
                session,
                "(entmake '((0 . \"ATTDEF\") (10 0.0 2.0 0.0) (40 . 2.5) (1 . \"D\") (3 . \"Prompt\") (2 . \"TAG1\") (70 . 0)))"
            );
            Value(session, "(entmake '((0 . \"ENDBLK\")))");
            Value(
                session,
                "(entmake '((0 . \"LTYPE\") (100 . \"AcDbSymbolTableRecord\") (100 . \"AcDbLinetypeTableRecord\") (2 . \"GHDASH\") (70 . 0) (3 . \"Dash\") (72 . 65) (73 . 2) (40 . 0.75) (49 . 0.5) (74 . 0) (49 . -0.25) (74 . 0)))"
            );
            return session;
        }

        // An INSERT of GHA with its one attribute and the end, made by entmake; the groups of the INSERT's and the attribute's own
        // lists are added to theirs.
        private static void MakeInsert(CadCommandSession session, string insertGroups, string attribGroups)
        {
            Value(
                session,
                "(entmake '((0 . \"INSERT\") (2 . \"GHA\") (66 . 1) " + insertGroups + " (10 20.0 0.0 0.0)))"
            );
            Value(
                session,
                "(entmake '((0 . \"ATTRIB\") "
                    + attribGroups
                    + " (10 20.0 2.0 0.0) (40 . 2.5) (1 . \"V1\") (2 . \"TAG1\") (70 . 0)))"
            );
            Value(session, "(entmake '((0 . \"SEQEND\")))");
        }

        // The linetype and colour groups of the INSERT, its attribute and its end, as the three lists give them.
        private static string Looks(CadCommandSession session)
        {
            Value(session, "(setq i (entlast) a (entnext i) s (entnext a))");
            return Value(
                session,
                "(mapcar '(lambda (e) (list (assoc 62 (entget e)) (assoc 6 (entget e)))) (list i a s))"
            );
        }

        [Fact]
        public void AnEntmadeAttribAndItsSeqend_TakeTheCurrentLinetype()
        {
            CadCommandSession session = WithAttributeBlock();
            MakeInsert(session, string.Empty, string.Empty);

            // AutoCAD 2021 (console, l2p2 pf): on the blank drawing (CELTYPE Continuous) the INSERT, the ATTRIB and the SEQEND all
            // list (6 . "Continuous")
            Assert.Equal(
                "((nil (6 . \"Continuous\")) (nil (6 . \"Continuous\")) (nil (6 . \"Continuous\")))",
                Looks(session)
            );
            Assert.Equal(
                "((-1 . <Entity name: *>) (0 . \"SEQEND\") (330 . <Entity name: *>) (5 . \"*\") (100 . \"AcDbEntity\") (67 . 0) (410 . \"Model\") (8 . \"0\") (6 . \"Continuous\") (-2 . <Entity name: *>))",
                Value(session, "(entget s)")
            );

            // and under CELTYPE GHDASH
            Value(session, "(setvar \"CELTYPE\" \"GHDASH\")");
            MakeInsert(session, string.Empty, string.Empty);
            Assert.Equal("((nil (6 . \"GHDASH\")) (nil (6 . \"GHDASH\")) (nil (6 . \"GHDASH\")))", Looks(session));
        }

        [Fact]
        public void TheSeqendTakesTheCurrentLinetype_NotTheInsertsOrTheAttributes()
        {
            CadCommandSession session = WithAttributeBlock();
            Value(session, "(setvar \"CELTYPE\" \"GHDASH\")");

            // AutoCAD 2021 (console, l2p2 pf): the INSERT given Continuous: the ATTRIB (none given) GHDASH, the SEQEND GHDASH
            MakeInsert(session, "(6 . \"Continuous\")", string.Empty);
            Assert.Equal("((nil (6 . \"Continuous\")) (nil (6 . \"GHDASH\")) (nil (6 . \"GHDASH\")))", Looks(session));

            // the ATTRIB given Continuous: it keeps it, the SEQEND still GHDASH
            MakeInsert(session, string.Empty, "(6 . \"Continuous\")");
            Assert.Equal("((nil (6 . \"GHDASH\")) (nil (6 . \"Continuous\")) (nil (6 . \"GHDASH\")))", Looks(session));
        }

        [Fact]
        public void UnderAByBlockOrByLayerLinetype_TheThreeListByBlockOrNothing()
        {
            CadCommandSession session = WithAttributeBlock();

            // AutoCAD 2021 (console, l2p2 pf): CELTYPE ByBlock: all three (6 . "ByBlock"); ByLayer: none lists a 6
            Value(session, "(setvar \"CELTYPE\" \"ByBlock\")");
            MakeInsert(session, string.Empty, string.Empty);
            Assert.Equal("((nil (6 . \"ByBlock\")) (nil (6 . \"ByBlock\")) (nil (6 . \"ByBlock\")))", Looks(session));
            Value(session, "(setvar \"CELTYPE\" \"ByLayer\")");
            MakeInsert(session, string.Empty, string.Empty);
            Assert.Equal("((nil nil) (nil nil) (nil nil))", Looks(session));
        }

        [Fact]
        public void UnderACurrentColour_TheInsertTheAttribAndTheSeqendListItBeforeTheLinetype()
        {
            CadCommandSession session = WithAttributeBlock();
            Value(session, "(setvar \"CELTYPE\" \"GHDASH\")");
            Value(session, "(setvar \"CECOLOR\" \"1\")");
            MakeInsert(session, string.Empty, string.Empty);

            // AutoCAD 2021 (console, l2p2 pf): (8 . "0") (62 . 1) (6 . "GHDASH") for the three
            Assert.Equal(
                "(((62 . 1) (6 . \"GHDASH\")) ((62 . 1) (6 . \"GHDASH\")) ((62 . 1) (6 . \"GHDASH\")))",
                Looks(session)
            );
            Assert.Contains("(8 . \"0\") (62 . 1) (6 . \"GHDASH\") (-2 . ", Value(session, "(entget s)"));
            Assert.Contains("(8 . \"0\") (62 . 1) (6 . \"GHDASH\") (100 . \"AcDbText\")", Value(session, "(entget a)"));
        }

        [Fact]
        public void TheSeqendOfTheInsertCommand_TakesTheCurrentLinetypeAndColourAndItsAttributeTheDefinitions()
        {
            CadCommandSession session = WithAttributeBlock();
            Value(session, "(setvar \"ATTREQ\" 1)");
            Value(session, "(setvar \"CELTYPE\" \"GHDASH\")");
            Value(session, "(setvar \"CECOLOR\" \"1\")");
            Value(session, "(command \"-INSERT\" \"GHA\" \"160,0\" 1 1 0 \"V8\")");

            // AutoCAD 2021 (console, l2p2 pf, row V8): the INSERT (62 . 1) (6 . "GHDASH"), the ATTRIB the look of its definition,
            // (6 . "Continuous") with no colour, the SEQEND (62 . 1) (6 . "GHDASH")
            Assert.Equal(
                "(((62 . 1) (6 . \"GHDASH\")) (nil (6 . \"Continuous\")) ((62 . 1) (6 . \"GHDASH\")))",
                Looks(session)
            );
        }

        [Fact]
        public void TheSeqendOfTheInsertCommand_UnderTheBlankDrawing_ListsContinuous()
        {
            CadCommandSession session = WithAttributeBlock();
            Value(session, "(setvar \"ATTREQ\" 1)");
            Value(session, "(command \"-INSERT\" \"GHA\" \"180,0\" 1 1 0 \"V9\")");

            // AutoCAD 2021 (console, l2p2 pf, row V9; feed dcd): (6 . "Continuous") on the SEQEND
            Assert.Equal(
                "((nil (6 . \"Continuous\")) (nil (6 . \"Continuous\")) (nil (6 . \"Continuous\")))",
                Looks(session)
            );
        }

        [Fact]
        public void AnEntmadeLine_TakesTheCurrentColourBeforeTheLinetype()
        {
            CadCommandSession session = WithAttributeBlock();
            Value(session, "(setvar \"CELTYPE\" \"GHDASH\")");
            Value(session, "(setvar \"CECOLOR\" \"1\")");
            Value(session, "(entmake '((0 . \"LINE\") (10 0.0 0.0 0.0) (11 10.0 0.0 0.0)))");

            // AutoCAD 2021 (console, l2p2 ph): (8 . "0") (62 . 1) (6 . "GHDASH") (100 . "AcDbLine")
            Assert.Contains(
                "(8 . \"0\") (62 . 1) (6 . \"GHDASH\") (100 . \"AcDbLine\")",
                Value(session, "(entget (entlast))")
            );

            // a 62 and a 6 given are kept (ph: (62 . 3) (6 . "Continuous")); a 62 of 256 is no colour and CECOLOR is not put in
            Value(
                session,
                "(entmake '((0 . \"LINE\") (6 . \"Continuous\") (62 . 3) (10 0.0 0.0 0.0) (11 10.0 0.0 0.0)))"
            );
            Assert.Equal(
                "((62 . 3) (6 . \"Continuous\"))",
                Value(session, "(list (assoc 62 (entget (entlast))) (assoc 6 (entget (entlast))))")
            );
            Value(session, "(entmake '((0 . \"LINE\") (62 . 256) (10 0.0 0.0 0.0) (11 10.0 0.0 0.0)))");
            Assert.Equal("nil", Value(session, "(assoc 62 (entget (entlast)))"));
        }

        [Fact]
        public void AnEntmadeLineUnderAByBlockColour_Lists62Of0()
        {
            CadCommandSession session = Start();
            Value(session, "(setvar \"CECOLOR\" \"BYBLOCK\")");
            Value(session, "(entmake '((0 . \"LINE\") (10 0.0 0.0 0.0) (11 10.0 0.0 0.0)))");

            // AutoCAD 2021 (console, l2p2 ph): CECOLOR BYBLOCK lists (62 . 0)
            Assert.Equal("(62 . 0)", Value(session, "(assoc 62 (entget (entlast)))"));
        }

        #endregion

        #region MTEXT extents

        [Fact]
        public void AnEntmadeMtext_ListsTheExtentsItsWordsTakeInTheDrawingsFont()
        {
            using (var fonts = new FontFolder())
            {
                fonts.Write("test.shx", ShxFiles.Unifont());
                CadDocument document = Blank();
                document.Database.TextStyleTable["Standard"].FileName = "test.shx";
                document.Fonts = new CadFonts(new[] { fonts.FullPath }, null, null);
                CadCommandSession session = Session(document);

                // AutoCAD 2021 (console, l2p2 pf): "mtext body" at 2.5 in txt lists (42 . 24.1667) (43 . 3.33333), whatever 42 and 43
                // the list gives (99.0 and 88.0 gave the same); the presenter measures the words in the drawing's own font, here a
                // unifont of the tests
                Value(
                    session,
                    "(entmake '((0 . \"MTEXT\") (100 . \"AcDbEntity\") (100 . \"AcDbMText\") (10 0.0 0.0 0.0) (40 . 4.0) (1 . \"IOI\") (71 . 1) (42 . 99.0) (43 . 88.0)))"
                );
                string handle = Value(session, "(cdr (assoc 5 (entget (entlast))))").Trim('"');
                var text = (DxfMText)document.FindEntity(handle);
                string searched = document.Database.FontSearchPath;
                document.Database.FontSearchPath = fonts.FullPath;
                double width = text.ActualWidth;
                double height = text.ActualHeight;
                document.Database.FontSearchPath = searched;
                Assert.True(width > 1.0 && width < 90.0, "the words are measured: " + width);
                Assert.True(height > 1.0 && height < 80.0, "the words are measured: " + height);

                string listed = Value(
                    session,
                    "(list (abs (- (cdr (assoc 42 (entget (entlast)))) "
                        + width.ToString("R", CultureInfo.InvariantCulture)
                        + ")) (abs (- (cdr (assoc 43 (entget (entlast)))) "
                        + height.ToString("R", CultureInfo.InvariantCulture)
                        + ")))"
                );
                Assert.Matches(@"^\(\d\.?\d*(e-\d+)? \d\.?\d*(e-\d+)?\)$", listed);
                Assert.DoesNotContain("99", Value(session, "(assoc 42 (entget (entlast)))"));
                Assert.DoesNotContain("88", Value(session, "(assoc 43 (entget (entlast)))"));
            }
        }

        #endregion

        #region The order of a hatch's loops

        private const string ThreeRectangles =
            "(progn (command \"RECTANG\" \"0,0\" \"10,10\") (setq r1 (entlast)) (command \"RECTANG\" \"20,0\" \"30,10\") (setq r2 (entlast)) (command \"RECTANG\" \"40,0\" \"50,10\") (setq r3 (entlast)) (princ))";

        // The 10 and 11 groups of the last entity, as probe pg's gh-loops prints them.
        private const string Loops =
            "(mapcar 'cdr (vl-remove-if-not '(lambda (x) (member (car x) '(10 11))) (entget (entlast))))";

        [Fact]
        public void TheLoopsOfAHatchOfSelectedObjects_AreInTheOrderTheyWereSelected()
        {
            CadCommandSession session = Start();
            Value(session, ThreeRectangles);

            // AutoCAD 2021 (console, l2p2 pg): -HATCH S r3 r1 r2 keeps the loops in that order, r1 r2 r3 in that order
            Value(session, "(command \"-HATCH\" \"P\" \"ANSI31\" 1 0 \"S\" r3 r1 r2 \"\" \"\")");
            Assert.Equal(
                "((0.0 0.0 0.0) (40.0 0.0 0.0) (50.0 0.0 0.0) (50.0 0.0 0.0) (50.0 10.0 0.0) (50.0 10.0 0.0) (40.0 10.0 0.0) (40.0 10.0 0.0) (40.0 0.0 0.0) (0.0 0.0 0.0) (10.0 0.0 0.0) (10.0 0.0 0.0) (10.0 10.0 0.0) (10.0 10.0 0.0) (0.0 10.0 0.0) (0.0 10.0 0.0) (0.0 0.0 0.0) (20.0 0.0 0.0) (30.0 0.0 0.0) (30.0 0.0 0.0) (30.0 10.0 0.0) (30.0 10.0 0.0) (20.0 10.0 0.0) (20.0 10.0 0.0) (20.0 0.0 0.0) (0.0 0.0 0.0))",
                Value(session, Loops)
            );
            Value(session, "(command \"-HATCH\" \"P\" \"ANSI31\" 1 0 \"S\" r1 r2 r3 \"\" \"\")");
            Assert.Equal(
                "((0.0 0.0 0.0) (0.0 0.0 0.0) (10.0 0.0 0.0) (10.0 0.0 0.0) (10.0 10.0 0.0) (10.0 10.0 0.0) (0.0 10.0 0.0) (0.0 10.0 0.0) (0.0 0.0 0.0) (20.0 0.0 0.0) (30.0 0.0 0.0) (30.0 0.0 0.0) (30.0 10.0 0.0) (30.0 10.0 0.0) (20.0 10.0 0.0) (20.0 10.0 0.0) (20.0 0.0 0.0) (40.0 0.0 0.0) (50.0 0.0 0.0) (50.0 0.0 0.0) (50.0 10.0 0.0) (50.0 10.0 0.0) (40.0 10.0 0.0) (40.0 10.0 0.0) (40.0 0.0 0.0) (0.0 0.0 0.0))",
                Value(session, Loops)
            );
        }

        [Fact]
        public void TheLoopsOfAHatchOfAll_AreTheNewestObjectFirst()
        {
            CadCommandSession session = Start();
            Value(session, ThreeRectangles);

            // AutoCAD 2021 (console, l2p2 pg): -HATCH S ALL lists r3, r2, r1 (feed hh: the second rectangle's loop first)
            Value(session, "(command \"-HATCH\" \"P\" \"ANSI31\" 1 0 \"S\" \"ALL\" \"\" \"\")");
            Assert.Equal(
                "((0.0 0.0 0.0) (40.0 0.0 0.0) (50.0 0.0 0.0) (50.0 0.0 0.0) (50.0 10.0 0.0) (50.0 10.0 0.0) (40.0 10.0 0.0) (40.0 10.0 0.0) (40.0 0.0 0.0) (20.0 0.0 0.0) (30.0 0.0 0.0) (30.0 0.0 0.0) (30.0 10.0 0.0) (30.0 10.0 0.0) (20.0 10.0 0.0) (20.0 10.0 0.0) (20.0 0.0 0.0) (0.0 0.0 0.0) (10.0 0.0 0.0) (10.0 0.0 0.0) (10.0 10.0 0.0) (10.0 10.0 0.0) (0.0 10.0 0.0) (0.0 10.0 0.0) (0.0 0.0 0.0) (0.0 0.0 0.0))",
                Value(session, Loops)
            );
        }

        [Fact]
        public void ARectangleInARectangle_IsListedInTheOrderSelectedWhicheverIsInside()
        {
            CadCommandSession session = Start();
            Value(session, "(command \"RECTANG\" \"0,0\" \"40,40\")");
            Value(session, "(setq ro (entlast))");
            Value(session, "(command \"RECTANG\" \"10,10\" \"20,20\")");
            Value(session, "(setq ri (entlast))");

            // AutoCAD 2021 (console, l2p2 pi): selected inner first: the loops are flagged 16 (the inner) then 1 (the outer); selected
            // outer first: 1 then 16
            Value(session, "(command \"-HATCH\" \"P\" \"ANSI31\" 1 0 \"S\" ri ro \"\" \"\")");
            Assert.Equal(
                "(2 16 1)",
                Value(
                    session,
                    "(cons (cdr (assoc 91 (entget (entlast)))) (mapcar 'cdr (vl-remove-if-not '(lambda (x) (= (car x) 92)) (entget (entlast)))))"
                )
            );
            Value(session, "(command \"-HATCH\" \"P\" \"ANSI31\" 1 0 \"S\" ro ri \"\" \"\")");
            Assert.Equal(
                "(2 1 16)",
                Value(
                    session,
                    "(cons (cdr (assoc 91 (entget (entlast)))) (mapcar 'cdr (vl-remove-if-not '(lambda (x) (= (car x) 92)) (entget (entlast)))))"
                )
            );
        }

        #endregion

        #region The tail of a layer's list

        private const string LayerTail =
            "(list (assoc 290 (entget (tblobjname \"LAYER\" \"{0}\"))) (assoc 370 (entget (tblobjname \"LAYER\" \"{0}\"))) (car (assoc 390 (entget (tblobjname \"LAYER\" \"{0}\")))) (car (assoc 347 (entget (tblobjname \"LAYER\" \"{0}\")))) (car (assoc 348 (entget (tblobjname \"LAYER\" \"{0}\")))))";

        private static string TailOf(CadCommandSession session, string layer) =>
            Value(session, LayerTail.Replace("{0}", layer));

        [Fact]
        public void ALayersList_EndsWithThePlotFlagTheLineweightAndTheThreeObjects()
        {
            CadCommandSession session = Start();

            // AutoCAD 2021 (console, l2p2 pf): layer 0 lists ... (6 . "Continuous") (290 . 1) (370 . -3) (390 . <ename>) (347 .
            // <ename>) (348 . <ename>)
            Assert.Equal("((290 . 1) (370 . -3) 390 347 348)", TailOf(session, "0"));
            Assert.Matches(
                @"\(2 \. ""0""\) \(70 \. 0\) \(62 \. 7\) \(6 \. ""Continuous""\) \(290 \. 1\) \(370 \. -3\) \(390 \. <Entity name: \*>\) \(347 \. <Entity name: \*>\) \(348 \. <Entity name: \*>\)\)$",
                Value(session, "(entget (tblobjname \"LAYER\" \"0\"))")
            );
        }

        [Fact]
        public void ALayerSetToNoPlotByTheLayerCommand_Lists290Of0()
        {
            CadCommandSession session = Start();
            Value(session, "(command \"-LAYER\" \"N\" \"GHP1\" \"\")");
            Assert.Equal("((290 . 1) (370 . -3) 390 347 348)", TailOf(session, "GHP1"));
            Value(session, "(command \"-LAYER\" \"P\" \"N\" \"GHP1\" \"\")");

            // AutoCAD 2021 (console, l2p2 pf): (290 . 0) after -LAYER Plot No plot
            Assert.Equal("((290 . 0) (370 . -3) 390 347 348)", TailOf(session, "GHP1"));
        }

        [Fact]
        public void TheLayersOfEntmake_ListThePlotFlagTheyWereGiven()
        {
            CadCommandSession session = Start();
            Value(
                session,
                "(entmake '((0 . \"LAYER\") (100 . \"AcDbSymbolTableRecord\") (100 . \"AcDbLayerTableRecord\") (2 . \"GHP2\") (70 . 0) (62 . 3) (6 . \"Continuous\") (290 . 0)))"
            );
            Value(
                session,
                "(entmake '((0 . \"LAYER\") (100 . \"AcDbSymbolTableRecord\") (100 . \"AcDbLayerTableRecord\") (2 . \"GHP3\") (70 . 0) (62 . 3) (6 . \"Continuous\") (290 . 1)))"
            );

            // AutoCAD 2021 (console, l2p2 pf): GHP2 (290 . 0), GHP3 (290 . 1), both then (370 . -3)
            Assert.Equal("((290 . 0) (370 . -3) 390 347 348)", TailOf(session, "GHP2"));
            Assert.Equal("((290 . 1) (370 . -3) 390 347 348)", TailOf(session, "GHP3"));
        }

        [Fact]
        public void TheMaterialAndVisualStyleOfTheLayers_AreTheSameObjectForEveryLayerAndTheStyleHasNoList()
        {
            CadCommandSession session = Start();
            Value(session, "(command \"-LAYER\" \"N\" \"GHP1\" \"\")");

            // AutoCAD 2021 (console, l2p2 pf): the 347, 348 and 390 of layer 0 and of GHP1 are equal enames, and (entget the 348)
            // is nil; the presenter keeps no material or visual style, so its 347 and 348 name no object (ours)
            Assert.Equal(
                "T",
                Value(
                    session,
                    "(equal (cdr (assoc 347 (entget (tblobjname \"LAYER\" \"0\")))) (cdr (assoc 347 (entget (tblobjname \"LAYER\" \"GHP1\")))))"
                )
            );
            Assert.Equal(
                "T",
                Value(
                    session,
                    "(equal (cdr (assoc 348 (entget (tblobjname \"LAYER\" \"0\")))) (cdr (assoc 348 (entget (tblobjname \"LAYER\" \"GHP1\")))))"
                )
            );
            Assert.Equal("nil", Value(session, "(entget (cdr (assoc 348 (entget (tblobjname \"LAYER\" \"0\")))))"));
        }

        [Fact]
        public void TblsearchOfALayer_StaysAtItsFiveGroups()
        {
            CadCommandSession session = Start();

            // AutoCAD 2021 (console, l2p2 pf): (tblsearch "LAYER" "0") is ((0 . "LAYER") (2 . "0") (70 . 0) (62 . 7) (6 . "Continuous")),
            // with no 290, 370, 390, 347 or 348
            Assert.Equal(
                "((0 . \"LAYER\") (2 . \"0\") (70 . 0) (62 . 7) (6 . \"Continuous\"))",
                Value(session, "(tblsearch \"LAYER\" \"0\")")
            );
        }

        #endregion
    }
}
