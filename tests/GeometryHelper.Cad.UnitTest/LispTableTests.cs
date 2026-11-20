using System.Linq;
using System.Text.RegularExpressions;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// <c>tblsearch</c>, <c>tblnext</c> and <c>tblobjname</c> typed at the command line, with the lists of AutoCAD 2021's
    /// accoreconsole (probe q5 of the L2 spec). The drawing has the layers of the probe, GHA of colour 1 and NEWLAYER made by
    /// <c>entmake</c>; an entity name is written <c>*</c>.
    /// </summary>
    public sealed class LispTableTests
    {
        private static CadCommandSession Start()
        {
            CadDocument document = Blank();
            document.Database.LayerTable.Add(new DxfLayerTableRecord { Name = "GHA", ColorIndex = 1 });
            return Session(document);
        }

        private static string Value(CadCommandSession session, string form)
        {
            int before = session.History.Count;
            session.Feed(form);
            string text = string.Join("\n", session.History.Skip(before + 1).Select(l => l.TrimEnd()));
            return Regex.Replace(text, @"<Entity name: [0-9A-Fa-f]+>", "<Entity name: *>");
        }

        [Fact]
        public void TblsearchOfALayer_GivesItsNameFlagsColourAndLinetype()
        {
            CadCommandSession session = Start();

            // AutoCAD 2021 (q5): (tblsearch "LAYER" "GHA") ((0 . "LAYER") (2 . "GHA") (70 . 0) (62 . 1) (6 . "Continuous"))
            Assert.Equal(
                "((0 . \"LAYER\") (2 . \"GHA\") (70 . 0) (62 . 1) (6 . \"Continuous\"))",
                Value(session, "(tblsearch \"LAYER\" \"GHA\")")
            );
        }

        [Fact]
        public void TblsearchOfALayerEntmakeMade_GivesColour7()
        {
            CadCommandSession session = Start();
            Value(session, "(entmake '((0 . \"LINE\") (8 . \"NEWLAYER\") (10 0.0 0.0 0.0) (11 1.0 1.0 0.0)))");

            // AutoCAD 2021 (q5): ((0 . "LAYER") (2 . "NEWLAYER") (70 . 0) (62 . 7) (6 . "Continuous"))
            Assert.Equal(
                "((0 . \"LAYER\") (2 . \"NEWLAYER\") (70 . 0) (62 . 7) (6 . \"Continuous\"))",
                Value(session, "(tblsearch \"LAYER\" \"NEWLAYER\")")
            );
        }

        [Fact]
        public void TheTableAndTheRecordAreNamedInAnyCase_AndTheRecordKeepsItsSpelling()
        {
            CadCommandSession session = Start();

            // AutoCAD 2021 (q5): (tblsearch "layer" "gha") gives the record of GHA
            Assert.Equal(
                "((0 . \"LAYER\") (2 . \"GHA\") (70 . 0) (62 . 1) (6 . \"Continuous\"))",
                Value(session, "(tblsearch \"layer\" \"gha\")")
            );
        }

        [Fact]
        public void ARecordTheTableHasNot_IsNil()
        {
            CadCommandSession session = Start();

            Assert.Equal("nil", Value(session, "(tblsearch \"LAYER\" \"NOPE\")")); // AutoCAD 2021 (q5)
        }

        [Fact]
        public void ALayerSwitchedOffHasANegativeColour_AndAFrozenOneTheFlag1()
        {
            CadDocument document = Blank();
            document.Database.LayerTable.Add(
                new DxfLayerTableRecord
                {
                    Name = "OFF",
                    ColorIndex = 3,
                    IsOff = true,
                }
            );
            document.Database.LayerTable.Add(
                new DxfLayerTableRecord
                {
                    Name = "ICE",
                    ColorIndex = 4,
                    IsFrozen = true,
                }
            );
            document.Database.LayerTable.Add(
                new DxfLayerTableRecord
                {
                    Name = "LOCK",
                    ColorIndex = 5,
                    IsLocked = true,
                }
            );
            CadCommandSession session = Session(document);

            // AutoCAD 2021 (spec D54): 62 negative for a layer off; 70 1 frozen, 4 locked
            Assert.Equal(
                "(62 . -3)",
                Regex.Match(Value(session, "(tblsearch \"LAYER\" \"OFF\")"), @"\(62 \. -?\d+\)").Value
            );
            Assert.Equal(
                "(70 . 1)",
                Regex.Match(Value(session, "(tblsearch \"LAYER\" \"ICE\")"), @"\(70 \. \d+\)").Value
            );
            Assert.Equal(
                "(70 . 4)",
                Regex.Match(Value(session, "(tblsearch \"LAYER\" \"LOCK\")"), @"\(70 \. \d+\)").Value
            );
        }

        [Fact]
        public void TblsearchOfTheStandardStyle_GivesItsTenGroups()
        {
            CadCommandSession session = Start();

            // AutoCAD 2021 (q5)
            Assert.Equal(
                "((0 . \"STYLE\") (2 . \"Standard\") (70 . 0) (40 . 0.0) (41 . 1.0) (50 . 0.0) (71 . 0) (42 . 2.5) (3 . \"txt\") (4 . \"\"))",
                Value(session, "(tblsearch \"STYLE\" \"Standard\")")
            );
        }

        [Fact]
        public void TblsearchOfTheContinuousLinetype_GivesItsGroups()
        {
            CadCommandSession session = Start();

            // AutoCAD 2021 (q5)
            Assert.Equal(
                "((0 . \"LTYPE\") (2 . \"Continuous\") (70 . 0) (3 . \"Solid line\") (72 . 65) (73 . 0) (40 . 0.0))",
                Value(session, "(tblsearch \"LTYPE\" \"Continuous\")")
            );
        }

        [Fact]
        public void TblsearchOfTheBlockOfModelSpace_IsNil()
        {
            CadCommandSession session = Start();

            Assert.Equal("nil", Value(session, "(tblsearch \"BLOCK\" \"*Model_Space\")")); // AutoCAD 2021 (q5)
        }

        [Fact]
        public void TblsearchOfABlock_GivesItsNameFlagsBasePointAndFirstEntity()
        {
            CadDocument document = Blank();
            var block = new DxfBlockTableRecord { Name = "BOLT" };
            document.Database.BlockTable.Add(block);
            block.AppendEntity(
                new DxfLine(
                    new GeometryHelper.Geometry.GeoPoint3(0, 0, 0),
                    new GeometryHelper.Geometry.GeoPoint3(1, 0, 0)
                )
            );
            CadCommandSession session = Session(document);

            // AutoCAD 2021 (spec D54): BLOCK (0 2 70 10 -2), -2 the first entity
            Assert.Equal(
                "((0 . \"BLOCK\") (2 . \"BOLT\") (70 . 0) (10 0.0 0.0 0.0) (-2 . <Entity name: *>))",
                Value(session, "(tblsearch \"BLOCK\" \"BOLT\")")
            );
            Assert.Equal(
                "\"LINE\"",
                Value(session, "(cdr (assoc 0 (entget (cdr (assoc -2 (tblsearch \"BLOCK\" \"BOLT\"))))))")
            );
        }

        [Fact]
        public void TblsearchOfTheStandardDimensionStyle_GivesTheGroupsOfAutoCadsList()
        {
            CadCommandSession session = Start();

            // AutoCAD 2021 (q5): the variables of R13 in AutoCAD's order with the text style of DIMTXSTY (340) as an entity name
            Assert.Equal(
                "((0 . \"DIMSTYLE\") (2 . \"Standard\") (70 . 0) (3 . \"\") (4 . \"\") (5 . \"\") (6 . \"\") (7 . \"\") (40 . 1.0) (41 . 0.18) (42 . 0.0625) (43 . 0.38) (44 . 0.18) (45 . 0.0) (46 . 0.0) (47 . 0.0) (48 . 0.0) (140 . 0.18) (141 . 0.09) (142 . 0.0) (143 . 25.4) (144 . 1.0) (145 . 0.0) (146 . 1.0) (147 . 0.09) (71 . 0) (72 . 0) (73 . 1) (74 . 1) (75 . 0) (76 . 0) (77 . 0) (78 . 0) (170 . 0) (171 . 2) (172 . 0) (173 . 0) (174 . 0) (175 . 0) (176 . 0) (177 . 0) (178 . 0) (270 . 2) (271 . 4) (272 . 4) (273 . 2) (274 . 2) (340 . <Entity name: *>) (275 . 0) (280 . 0) (281 . 0) (282 . 0) (283 . 1) (284 . 0) (285 . 0) (286 . 0) (287 . 3) (288 . 0))",
                Value(session, "(tblsearch \"DIMSTYLE\" \"Standard\")")
            );
        }

        [Fact]
        public void TheTextStyleOfADimensionStyle_IsTheEntityNameOfTheStyle()
        {
            CadCommandSession session = Start();

            Assert.Equal(
                "T",
                Value(
                    session,
                    "(eq (cdr (assoc 340 (tblsearch \"DIMSTYLE\" \"Standard\"))) (tblobjname \"STYLE\" \"Standard\"))"
                )
            );
        }

        [Fact]
        public void TblsearchOfATableAutoCadHasNot_IsAnErrorWithItsArguments()
        {
            CadCommandSession session = Start();

            // AutoCAD 2021 (q5): (tblsearch "FOO" "x") gives AutoCAD rejected function: invalid table function argument(s): "FOO" "x"
            Assert.Equal(
                "; error: AutoCAD rejected function: invalid table function argument(s): \"FOO\" \"x\"",
                Value(session, "(tblsearch \"FOO\" \"x\")")
            );
        }

        [Fact]
        public void TblnextWithRewind_GivesTheFirstRecord_AndEachCallTheNextUntilNil()
        {
            CadCommandSession session = Start();

            // AutoCAD 2021 (q5): (tblnext "LAYER" T) the layer 0, (tblnext "LAYER") GHA
            Assert.Equal(
                "((0 . \"LAYER\") (2 . \"0\") (70 . 0) (62 . 7) (6 . \"Continuous\"))",
                Value(session, "(tblnext \"LAYER\" T)")
            );
            Assert.Equal(
                "((0 . \"LAYER\") (2 . \"GHA\") (70 . 0) (62 . 1) (6 . \"Continuous\"))",
                Value(session, "(tblnext \"LAYER\")")
            );
            Assert.Equal("nil", Value(session, "(tblnext \"LAYER\")"));
            Assert.Equal("\"0\"", Value(session, "(cdr (assoc 2 (tblnext \"LAYER\" T)))"));
        }

        [Fact]
        public void TblsearchWithSetnext_MakesTblnextGiveTheRecordAfterIt()
        {
            CadCommandSession session = Start();

            Value(session, "(tblsearch \"LAYER\" \"0\" T)");

            Assert.Equal("\"GHA\"", Value(session, "(cdr (assoc 2 (tblnext \"LAYER\")))"));
        }

        [Fact]
        public void TblobjnameAndEntgetOfIt_GiveTheWholeRecord()
        {
            CadCommandSession session = Start();

            // AutoCAD 2021 (q5): (tblobjname "LAYER" "0") an entity name; its entget the whole record, with the owner (330), the
            // handle (5), the subclass markers and the plot style (390). The presenter's record has no 290, 370, 347 or 348.
            Assert.Equal("<Entity name: *>", Value(session, "(tblobjname \"LAYER\" \"0\")"));
            Assert.Equal(
                "((-1 . <Entity name: *>) (0 . \"LAYER\") (330 . <Entity name: *>) (5 . \"10\") (100 . \"AcDbSymbolTableRecord\") (100 . \"AcDbLayerTableRecord\") (2 . \"0\") (70 . 0) (62 . 7) (6 . \"Continuous\") (390 . <Entity name: *>))",
                Value(session, "(entget (tblobjname \"LAYER\" \"0\"))")
            );
            Assert.Equal("nil", Value(session, "(tblobjname \"LAYER\" \"NOPE\")"));
        }

        [Fact]
        public void TblsearchOfTheOtherTables_GivesTheirRecords()
        {
            CadCommandSession session = Start();

            Assert.Equal("((0 . \"APPID\") (2 . \"ACAD\") (70 . 0))", Value(session, "(tblsearch \"APPID\" \"ACAD\")"));
            Assert.StartsWith(
                "((0 . \"VPORT\") (2 . \"*Active\") (70 . 0) (10 0.0 0.0)",
                Value(session, "(tblsearch \"VPORT\" \"*Active\")")
            );
            Assert.Equal("nil", Value(session, "(tblsearch \"UCS\" \"A\")"));
            Assert.Equal("nil", Value(session, "(tblsearch \"VIEW\" \"A\")"));
        }
    }
}
