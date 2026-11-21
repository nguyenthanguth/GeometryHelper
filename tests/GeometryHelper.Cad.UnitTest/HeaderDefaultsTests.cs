using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// A DXF whose header leaves out MEASUREMENT, the dimension variables, DIMSTYLE or CELTYPE reads them as AutoCAD 2021
    /// does (probed in its console on a blank drawing: MEASUREMENT 0, DIMASZ 0.18, DIMTXT 0.18, DIMTAD 0, DIMDEC 4, DIMSTYLE
    /// Standard, CELTYPE Continuous; a header of MEASUREMENT 1 without the dimension variables gave the same imperial ones),
    /// and a line drawn on it carries the linetype Continuous as its own.
    /// </summary>
    public sealed class HeaderDefaultsTests
    {
        // A drawing of AutoCAD 2000's DXF with the linetype Continuous, the layer 0 and the dimension style Standard as a bare
        // record, as AutoCAD writes a blank drawing's, and the header lines given.
        private static string Dxf(string version, string header)
        {
            var text = new StringBuilder();
            text.Append("0\nSECTION\n2\nHEADER\n9\n$ACADVER\n1\n").Append(version).Append('\n').Append(header);
            text.Append("0\nENDSEC\n0\nSECTION\n2\nTABLES\n");
            text.Append(
                "0\nTABLE\n2\nLTYPE\n70\n1\n0\nLTYPE\n2\nContinuous\n70\n0\n3\nSolid line\n72\n65\n73\n0\n40\n0\n0\nENDTAB\n"
            );
            text.Append("0\nTABLE\n2\nLAYER\n70\n1\n0\nLAYER\n2\n0\n70\n0\n62\n7\n6\nContinuous\n0\nENDTAB\n");
            text.Append("0\nTABLE\n2\nDIMSTYLE\n70\n1\n0\nDIMSTYLE\n2\nStandard\n70\n0\n0\nENDTAB\n0\nENDSEC\n");
            text.Append("0\nSECTION\n2\nENTITIES\n0\nENDSEC\n0\nEOF\n");
            return text.ToString();
        }

        private static CadDocument Open(string header, string version = "AC1015") =>
            CadDocument.Open(new MemoryStream(Encoding.UTF8.GetBytes(Dxf(version, header))), "blank.dxf");

        // What a form gives, as the line after it.
        private static string Value(CadCommandSession session, string form)
        {
            int before = session.History.Count;
            session.Feed(form);
            return string.Join("\n", session.History.Skip(before + 1).Select(l => l.TrimEnd()));
        }

        private const string Units = "9\n$HANDSEED\n5\n100\n9\n$INSUNITS\n70\n4\n";

        [Theory]
        [InlineData("MEASUREMENT", "0")]
        [InlineData("DIMASZ", "0.18")]
        [InlineData("DIMTXT", "0.18")]
        [InlineData("DIMTAD", "0")]
        [InlineData("DIMDEC", "4")]
        [InlineData("DIMDSEP", "\".\"")]
        [InlineData("DIMCEN", "0.09")]
        [InlineData("DIMEXE", "0.18")]
        [InlineData("DIMZIN", "0")]
        public void AHeaderWithNoDimensionVariables_ReadsAutoCadsImperialOnes(string variable, string expected)
        {
            CadCommandSession session = Session(Open(Units));

            Assert.Equal(expected, Value(session, "(getvar \"" + variable + "\")"));
        }

        [Fact]
        public void AHeaderWithNoDimstyleOrCeltype_ReadsStandardAndContinuous()
        {
            CadCommandSession session = Session(Open(Units));

            Assert.Equal("\"Standard\"", Value(session, "(getvar \"DIMSTYLE\")"));
            Assert.Equal("\"Continuous\"", Value(session, "(getvar \"CELTYPE\")"));
        }

        [Fact]
        public void TheStandardStyleRecord_IsAutoCadsOwn_AsTblsearchGivesItInTheProbe()
        {
            CadCommandSession session = Session(Open(Units));

            // The groups of the record of q5: 41 and 140 the arrowhead and text, 143 the alternate factor, 271 the decimals.
            Assert.Equal(
                "(0.18 0.18 0.09 25.4 4 4 2 1)",
                Value(
                    session,
                    "(mapcar '(lambda (c) (cdr (assoc c (tblsearch \"DIMSTYLE\" \"Standard\")))) '(41 140 141 143 271 272 274 283))"
                )
            );
        }

        [Fact]
        public void AMetricHeaderWithNoDimensionVariables_ReadsTheSameImperialOnes()
        {
            CadCommandSession session = Session(Open(Units + "9\n$MEASUREMENT\n70\n1\n"));

            Assert.Equal("1", Value(session, "(getvar \"MEASUREMENT\")"));
            Assert.Equal("0.18", Value(session, "(getvar \"DIMASZ\")"));
            Assert.Equal("\"Standard\"", Value(session, "(getvar \"DIMSTYLE\")"));
        }

        [Fact]
        public void AVariableTheHeaderGives_IsKept_AndTheOthersAreAutoCadsOwn()
        {
            CadCommandSession session = Session(
                Open(Units + "9\n$MEASUREMENT\n70\n1\n9\n$DIMASZ\n40\n2.5\n9\n$CELTYPE\n6\nByLayer\n")
            );

            Assert.Equal("2.5", Value(session, "(getvar \"DIMASZ\")"));
            Assert.Equal("\"ByLayer\"", Value(session, "(getvar \"CELTYPE\")"));
            Assert.Equal("0.18", Value(session, "(getvar \"DIMTXT\")"));
        }

        [Fact]
        public void ALineDrawnOnTheDrawing_CarriesContinuousAsItsOwnLinetype()
        {
            CadCommandSession session = Session(Open(Units));

            Run(session, "LINE", "0,0", "10,0", Enter);

            Assert.Equal("(6 . \"Continuous\")", Value(session, "(assoc 6 (entget (entlast)))"));
        }

        [Fact]
        public void TheValuesAreWrittenIntoTheHeader_SoASavedDrawingHoldsThem()
        {
            CadDocument document = Open(Units);

            Assert.Equal("0.18", document.Database.GetHeader("$DIMASZ").Single().Value.Trim());
            Assert.Equal("Continuous", document.Database.GetHeader("$CELTYPE").Single().Value.Trim());
            Assert.Equal("Standard", document.Database.GetHeader("$DIMSTYLE").Single().Value.Trim());
            Assert.Equal("0", document.Database.GetHeader("$MEASUREMENT").Single().Value.Trim());
        }

        [Fact]
        public void ADrawingOlderThanR2000_IsLeftAsItIs()
        {
            CadDocument document = Open(string.Empty, "AC1009");

            Assert.Null(document.Database.GetHeader("$MEASUREMENT"));
            Assert.Null(document.Database.GetHeader("$CELTYPE"));
            Assert.Null(document.Database.GetHeader("$DIMASZ"));
        }

        [Fact]
        public void ADrawingMadeInCode_KeepsTheLibrarysOwnDefaults()
        {
            CadCommandSession session = Session(Blank());

            Assert.Equal("\"ISO-25\"", Value(session, "(getvar \"DIMSTYLE\")"));
            Assert.Equal("2.5", Value(session, "(getvar \"DIMASZ\")"));
            Assert.Equal("\"ByLayer\"", Value(session, "(getvar \"CELTYPE\")"));
        }
    }
}
