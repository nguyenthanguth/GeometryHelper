using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// LIST of a dimension's overrides, two more things checked against AutoCAD 2021 in accoreconsole (probes of 13 November):
    /// a number longer than twelve characters is written in scientific form, as 1.000500E+03 (six places, five for a negative
    /// one), and an override that names a linetype or a block or a text style that is not there is written as AutoCAD writes
    /// it, or not at all.
    /// </summary>
    public class ListOverridesFollowUps2Tests
    {
        private static CadCommandSession Start(CadDocument document, params object[] inputs)
        {
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            return Run(session, inputs);
        }

        private static CadDocument Drawing(int lunits, int luprec)
        {
            CadDocument document = Blank();
            document.Database.Lunits = lunits;
            document.Database.Luprec = luprec;
            document.Database.Aunits = 0;
            document.Database.Auprec = 0;
            return document;
        }

        /// <summary>A rotated dimension in model space with an empty block of its own, given the overrides of its list DSTYLE.</summary>
        private static string Add(CadDocument document, params string[] overrides)
        {
            var dimension = new DxfRotatedDimension(
                0.0,
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(100, 0, 0),
                new GeoPoint3(50, 20, 0),
                string.Empty,
                null
            );
            var picture = new DxfBlockTableRecord { Name = "*D" };
            document.Database.BlockTable.Add(picture);
            dimension.DimBlock = picture;
            document.Database.ModelSpace.AppendEntity(dimension);
            var data = new List<DxfTypedValue>
            {
                new DxfTypedValue(1001, "ACAD"),
                new DxfTypedValue(1000, "DSTYLE"),
                new DxfTypedValue(1002, "{"),
            };

            foreach (string given in overrides)
            {
                string[] parts = given.Split(new[] { ':' }, 3);
                data.Add(new DxfTypedValue(1070, parts[0]));
                data.Add(new DxfTypedValue(int.Parse(parts[1], CultureInfo.InvariantCulture), parts[2]));
            }

            data.Add(new DxfTypedValue(1002, "}"));
            dimension.XData = data;
            document.Regen();
            return dimension.Handle;
        }

        private static List<string> Overrides(CadDocument document, string handle)
        {
            List<string> history = Start(document, "LIST", new[] { handle }, Enter).History.ToList();
            int from = history.FindIndex(l => l.StartsWith("dimension style: ", StringComparison.Ordinal));
            int to = history.FindIndex(l => l.StartsWith("Annotative: ", StringComparison.Ordinal));
            Assert.True(from >= 0 && to > from);
            return history.Skip(from + 1).Take(to - from - 1).ToList();
        }

        /// <summary>
        /// AutoCAD 2021 writes a distance of an override in scientific form where its text would be longer than twelve
        /// characters (the unit marks and the minus sign counted), the mantissa to six places and five for a negative number:
        /// DIMASZ 1 000.5 at eight places is 1.000500E+03, minus 100.5 is -1.00500E+02, and 100.5 stays 100.50000000. Architectural
        /// and fractional units are written whatever their length.
        /// </summary>
        [Theory]
        [InlineData(2, 8, 1000.5, "1.000500E+03")]
        [InlineData(2, 8, -100.5, "-1.00500E+02")]
        [InlineData(2, 8, 100.5, "100.50000000")]
        [InlineData(2, 8, 99999.0, "9.999900E+04")]
        [InlineData(2, 8, 1000000.0, "1.000000E+06")]
        [InlineData(2, 6, 99999.0, "99999.000000")]
        [InlineData(2, 6, -99999.0, "-9.99990E+04")]
        [InlineData(2, 4, 12345678.9, "1.2346E+07")]
        [InlineData(2, 4, 123456.789, "123456.7890")]
        [InlineData(1, 8, 100.5, "1.005000E+02")]
        [InlineData(1, 6, -100.5, "-1.00500E+02")]
        [InlineData(3, 8, 12.25, "1.225000E+01")]
        [InlineData(3, 6, 1000.5, "1.000500E+03")]
        [InlineData(3, 6, 100.5, "8'-4.500000\"")]
        [InlineData(3, 4, 99999.0, "9.9999E+04")]
        [InlineData(3, 4, 1000.5, "83'-4.5000\"")]
        [InlineData(3, 8, 0.0, "0.00000000\"")]
        [InlineData(4, 8, 1000.5, "83'-4 1/2\"")]
        [InlineData(5, 8, 1000.5, "1000 1/2")]
        public void ListOfADistanceOverride_WritesAScientificNumberWhereItIsTooLong_AsAutoCADDoes(
            int lunits,
            int luprec,
            double value,
            string expected
        )
        {
            CadDocument document = Drawing(lunits, luprec);
            string handle = Add(document, "41:1040:" + value.ToString("R", CultureInfo.InvariantCulture));

            Assert.Equal(
                new[] { "dimension style overrides:", " DIMASZ      " + expected },
                Overrides(document, handle)
            );
        }

        /// <summary>
        /// The scale-like overrides, plain numbers to the drawing's places in any units, keep to the same twelve characters:
        /// DIMSCALE 1 000.5 is 1000.5000 at four places and 1.000500E+03 at eight, minus 100.5 at eight is -1.00500E+02.
        /// </summary>
        [Theory]
        [InlineData(2, 8, 1000.5, "1.000500E+03")]
        [InlineData(2, 4, 1000.5, "1000.5000")]
        [InlineData(3, 8, -100.5, "-1.00500E+02")]
        [InlineData(4, 8, 1000000.0, "1.000000E+06")]
        [InlineData(2, 4, 1000000.0, "1000000.0000")]
        public void ListOfAScaleOverride_WritesAScientificNumberWhereItIsTooLong_AsAutoCADDoes(
            int lunits,
            int luprec,
            double value,
            string expected
        )
        {
            CadDocument document = Drawing(lunits, luprec);
            string handle = Add(document, "40:1040:" + value.ToString("R", CultureInfo.InvariantCulture));

            Assert.Equal(
                new[] { "dimension style overrides:", " DIMSCALE    " + expected },
                Overrides(document, handle)
            );
        }

        /// <summary>
        /// A linetype override of handle 0, or of the handle of an object that is no linetype (the layer 0's), is BYBLOCK in
        /// AutoCAD 2021's LIST; one of a handle no object has is not listed, nor is a text style of none, of no object or of
        /// a layer, nor a block of no object or of a layer; a block of handle 0 is ClosedFilled.
        /// </summary>
        [Theory]
        [InlineData("345:1005:0", " DIMLTYPE    BYBLOCK")]
        [InlineData("346:1005:0", " DIMLTEX1    BYBLOCK")]
        [InlineData("347:1005:0", " DIMLTEX2    BYBLOCK")]
        [InlineData("345:1005:LAYER", " DIMLTYPE    BYBLOCK")]
        [InlineData("347:1005:LAYER", " DIMLTEX2    BYBLOCK")]
        [InlineData("345:1005:FFFF", null)]
        [InlineData("346:1005:ffff", null)]
        [InlineData("340:1005:0", null)]
        [InlineData("340:1005:FFFF", null)]
        [InlineData("340:1005:LAYER", null)]
        [InlineData("342:1005:0", " DIMBLK      ClosedFilled")]
        [InlineData("343:1005:FFFF", null)]
        [InlineData("341:1005:LAYER", null)]
        public void ListOfAnOverrideNamingNoRecord_WritesItAsAutoCADDoes(string given, string expected)
        {
            CadDocument document = Drawing(2, 4);
            string layer = document.Database.LayerTable["0"].Handle;
            string handle = Add(document, given.Replace("LAYER", layer));

            Assert.Equal(
                expected == null ? new string[0] : new[] { "dimension style overrides:", expected },
                Overrides(document, handle)
            );
        }
    }
}
