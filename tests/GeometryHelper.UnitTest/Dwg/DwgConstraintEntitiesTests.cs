using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using GeometryHelper.UnitTest.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dwg
{
    /// <summary>
    /// The entities a dynamic block with constraints keeps in its block, which AutoCAD 2021's AUDIT counted as errors where a
    /// DWG written to R2018 lost what they hold: the identifiers of the vertices of a polyline, which the constraints name
    /// their edges by, and the flags of a dimensional constraint, which has no block of its picture.
    /// </summary>
    public class DwgConstraintEntitiesTests
    {
        private static string Lines(string groups) =>
            string.Concat(
                groups
                    .Split('|')
                    .Select(g => g.Substring(0, g.IndexOf('=')) + "\n" + g.Substring(g.IndexOf('=') + 1) + "\n")
            );

        /// <summary>A drawing of ezdxf with the records <paramref name="records"/> gives (from the handle of model space) at the end of its ENTITIES.</summary>
        private static string Drawing(Func<string, string> records)
        {
            string text = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018).ToText().Replace("\r\n", "\n");
            string owner = DwgAnswerKey
                .Parse(Encoding.UTF8.GetBytes(text))
                .First(r => r.Type == "LINE" && r.Section == "ENTITIES")
                .Owner;
            int at = text.IndexOf(
                "  0\nENDSEC",
                text.IndexOf("  2\nENTITIES", StringComparison.Ordinal),
                StringComparison.Ordinal
            );
            return text.Insert(at, records(owner));
        }

        private static DwgAnswerKey.Record Back(string text, string handle) =>
            DwgAnswerKey
                .Parse(DwgReader.ToDxf(DwgWriter.ToDwg(text, "AC1032")))
                .Single(r => r.Key.EndsWith(":" + handle, StringComparison.Ordinal));

        private static double Number(string text) =>
            double.Parse(text, System.Globalization.CultureInfo.InvariantCulture);

        private static List<string> Values(DwgAnswerKey.Record record, int code) =>
            record.Groups.Where(g => g.Key == code).Select(g => g.Value.Trim()).ToList();

        [Fact]
        public void ThePolylineOfAConstraintBlock_KeepsTheIdentifiersOfItsVertices()
        {
            string text = Drawing(owner =>
                "  0\nLWPOLYLINE\n  5\nF501\n330\n"
                + owner
                + "\n"
                + Lines(
                    "100=AcDbEntity|8=0|100=AcDbPolyline|90=3|70=0|43=0.0|10=0.0|20=0.0|91=27|10=10.0|20=0.0|91=28|42=0.5"
                        + "|10=10.0|20=10.0|91=31"
                )
            );
            DwgAnswerKey.Record polyline = Back(text, "F501");

            Assert.Equal(new[] { "27", "28", "31" }, Values(polyline, 91));
            Assert.Equal(new[] { 0.0, 10.0, 10.0 }, Values(polyline, 10).Select(Number));
            Assert.Equal(new[] { 0.5 }, Values(polyline, 42).Select(Number));
        }

        [Fact]
        public void ThePolylineWithNoIdentifiers_IsWrittenAsBefore()
        {
            string text = Drawing(owner =>
                "  0\nLWPOLYLINE\n  5\nF502\n330\n"
                + owner
                + "\n"
                + Lines("100=AcDbEntity|8=0|100=AcDbPolyline|90=2|70=0|43=0.0|10=0.0|20=0.0|10=10.0|20=0.0")
            );

            Assert.Empty(Values(Back(text, "F502"), 91));
        }

        private const string Dimension =
            "100=AcDbEntity|8=*ADSK_CONSTRAINTS|100=AcDbDimension|280=0|10=5.0|20=5.0|30=0.0|11=2.0|21=2.0|31=0.0"
            + "|71=5|42=10.0|73=0|74=0|75=0|3=Standard|100=AcDbAlignedDimension|13=0.0|23=0.0|33=0.0|14=10.0|24=0.0|34=0.0"
            + "|100=AcDbRotatedDimension";

        [Fact]
        public void ADimensionalConstraint_HasNeitherABlockNorATypeWhenItIsReadBack()
        {
            // AutoCAD's DXF gives a dimensional constraint (no block of its picture) with neither the block (2) nor a type (70)
            // where that is 0; written with the flag of a block it was read back with both, and AUDIT counted it as an error.
            string text = Drawing(owner => "  0\nDIMENSION\n  5\nF601\n330\n" + owner + "\n" + Lines(Dimension));
            DwgAnswerKey.Record dimension = Back(text, "F601");

            Assert.DoesNotContain(dimension.Groups, g => g.Key == 2);
            Assert.DoesNotContain(dimension.Groups, g => g.Key == 70);
        }

        [Fact]
        public void ADimensionalConstraintWithItsTextMoved_KeepsItsTypeWithoutTheBitOfAnOrdinaryDimension()
        {
            // The radius of a block's constraints, with the text where its user put it: type 4 and the bit 128, not 32.
            string radial =
                "100=AcDbEntity|8=*ADSK_CONSTRAINTS|100=AcDbDimension|280=0|10=5.0|20=5.0|30=0.0|11=2.0|21=2.0|31=0.0|70=132"
                + "|71=5|42=10.0|73=0|74=0|75=0|3=Standard|100=AcDbRadialDimension|15=0.0|25=0.0|35=0.0|40=1.0";
            string text = Drawing(owner => "  0\nDIMENSION\n  5\nF602\n330\n" + owner + "\n" + Lines(radial));
            DwgAnswerKey.Record dimension = Back(text, "F602");

            Assert.Equal(new[] { "132" }, Values(dimension, 70));
            Assert.DoesNotContain(dimension.Groups, g => g.Key == 2);
        }
    }
}
