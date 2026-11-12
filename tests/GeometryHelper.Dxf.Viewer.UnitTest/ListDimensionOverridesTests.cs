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
    /// LIST of a dimension that overrides its style, each line checked against AutoCAD 2021 in accoreconsole (plugins GhAl1
    /// and GhAl2): "dimension style overrides:" after the style's name, then each variable overridden by name in alphabetical
    /// order, its value as AutoCAD writes a distance, an angle, a colour, a flag, an arrowhead, a linetype, a text style, a
    /// lineweight, a letter, a text and a whole number. The overrides are written as AutoCAD's API wrote them, list DSTYLE of
    /// the extended data of ACAD, group for group.
    /// </summary>
    public class ListDimensionOverridesTests
    {
        private static CadCommandSession Start(CadDocument document, params object[] inputs)
        {
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            return Run(session, inputs);
        }

        /// <summary>A blank drawing of decimal lengths to 4 places and angles to whole degrees, as AutoCAD's console drawing.</summary>
        private static CadDocument Drawing()
        {
            CadDocument document = Blank();
            document.Database.Lunits = 2;
            document.Database.Luprec = 4;
            document.Database.Aunits = 0;
            document.Database.Auprec = 0;
            return document;
        }

        /// <summary>
        /// Adds a dimension to model space, with an empty block of its own to draw it with, and gives it the overrides: each a
        /// group of the variable, the code its value is written with and the value, as AutoCAD's API wrote them.
        /// </summary>
        private static string Add(CadDocument document, DxfDimension dimension, params string[] overrides)
        {
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

        /// <summary>The lines LIST writes of one dimension from its style's name on, up to "Annotative:".</summary>
        private static List<string> Overrides(CadDocument document, string handle)
        {
            List<string> history = Start(document, "LIST", new[] { handle }, Enter).History.ToList();
            int from = history.FindIndex(l => l.StartsWith("dimension style: ", StringComparison.Ordinal));
            int to = history.FindIndex(l => l.StartsWith("Annotative: ", StringComparison.Ordinal));
            Assert.True(from >= 0 && to > from);
            return history.Skip(from + 1).Take(to - from - 1).ToList();
        }

        private static string Handle(DxfDBObject record) => record.Handle;

        private static DxfTextStyleTableRecord TextStyle(CadDocument document, string name)
        {
            var style = new DxfTextStyleTableRecord { Name = name, FileName = "simplex.shx" };
            document.Database.TextStyleTable.Add(style);
            return style;
        }

        private static DxfBlockTableRecord Arrow(CadDocument document, string name) =>
            CadArrowheads.Ensure(document.Database, name);

        public static IEnumerable<object[]> Kinds() =>
            new[]
            {
                "rotated",
                "aligned",
                "angular",
                "three-point angular",
                "arc",
                "radial",
                "diametric",
                "ordinate",
                "jogged",
            }.Select(k => new object[] { k });

        private static DxfDimension Of(string kind)
        {
            switch (kind)
            {
                case "rotated":
                    return new DxfRotatedDimension(
                        0.0,
                        new GeoPoint3(0, 0, 0),
                        new GeoPoint3(100, 0, 0),
                        new GeoPoint3(50, 20, 0),
                        string.Empty,
                        null
                    );
                case "aligned":
                    return new DxfAlignedDimension(
                        new GeoPoint3(0, 100, 0),
                        new GeoPoint3(100, 150, 0),
                        new GeoPoint3(40, 170, 0),
                        string.Empty,
                        null
                    );
                case "angular":
                    return new DxfLineAngularDimension2(
                        new GeoPoint3(200, 0, 0),
                        new GeoPoint3(300, 0, 0),
                        new GeoPoint3(200, 0, 0),
                        new GeoPoint3(270, 70, 0),
                        new GeoPoint3(260, 30, 0),
                        string.Empty,
                        null
                    );
                case "three-point angular":
                    return new DxfPoint3AngularDimension(
                        new GeoPoint3(400, 0, 0),
                        new GeoPoint3(500, 0, 0),
                        new GeoPoint3(400, 100, 0),
                        new GeoPoint3(470, 70, 0),
                        string.Empty,
                        null
                    );
                case "arc":
                    return new DxfArcDimension(
                        new GeoPoint3(600, 0, 0),
                        new GeoPoint3(700, 0, 0),
                        new GeoPoint3(600, 100, 0),
                        new GeoPoint3(680, 80, 0),
                        string.Empty,
                        null
                    );
                case "radial":
                    return new DxfRadialDimension(
                        new GeoPoint3(800, 0, 0),
                        new GeoPoint3(850, 0, 0),
                        10.0,
                        string.Empty,
                        null
                    );
                case "diametric":
                    return new DxfDiametricDimension(
                        new GeoPoint3(950, 0, 0),
                        new GeoPoint3(1050, 0, 0),
                        10.0,
                        string.Empty,
                        null
                    );
                case "ordinate":
                    return new DxfOrdinateDimension(
                        true,
                        new GeoPoint3(1100, 0, 0),
                        new GeoPoint3(1100, 50, 0),
                        string.Empty,
                        null
                    );
                default:
                    return new DxfRadialDimensionLarge(
                        new GeoPoint3(1200, 0, 0),
                        new GeoPoint3(1250, 40, 0),
                        new GeoPoint3(1180, -50, 0),
                        new GeoPoint3(1210, -10, 0),
                        0.5,
                        string.Empty,
                        null
                    );
            }
        }

        /// <summary>
        /// AutoCAD 2021 lists the same eight lines for each type of dimension given DIMTAD 1, DIMJOGANG 30 degrees, DIMTXSTY
        /// GHSTYLE, DIMBLK _DOT, DIMSE1 on, DIMCLRD 1, DIMASZ 5 and DIMTXT 3.5, in that order in its data: by name, between the
        /// style and "Annotative: No".
        /// </summary>
        [Theory]
        [MemberData(nameof(Kinds))]
        public void ListOfADimensionOfEachType_WritesItsOverridesByName_AsAutoCADDoes(string kind)
        {
            CadDocument document = Drawing();
            string style = Handle(TextStyle(document, "GHSTYLE"));
            string dot = Handle(Arrow(document, "Dot"));
            string handle = Add(
                document,
                Of(kind),
                "77:1070:1",
                "50:1040:0.523598775598299",
                "340:1005:" + style,
                "342:1005:" + dot,
                "75:1070:1",
                "176:1070:1",
                "41:1040:5",
                "140:1040:3.5"
            );

            Assert.Equal(
                new[]
                {
                    "dimension style overrides:",
                    " DIMASZ      5.0000",
                    " DIMBLK      Dot",
                    " DIMCLRD     1 (red)",
                    " DIMJOGANG   30",
                    " DIMSE1      On",
                    " DIMTAD      1",
                    " DIMTXSTY    GHSTYLE",
                    " DIMTXT      3.5000",
                },
                Overrides(document, handle)
            );
        }

        [Fact]
        public void ListOfADimensionOfNoOverride_WritesNoOverrideLines()
        {
            CadDocument document = Drawing();
            var dimension = (DxfRotatedDimension)Of("rotated");
            var picture = new DxfBlockTableRecord { Name = "*D" };
            document.Database.BlockTable.Add(picture);
            dimension.DimBlock = picture;
            document.Database.ModelSpace.AppendEntity(dimension);
            document.Regen();

            Assert.Empty(Overrides(document, dimension.Handle));
        }

        /// <summary>
        /// Every variable a dimension can override, as AutoCAD 2021 listed the dimension its API gave all of them: distances to
        /// $LUPREC places, DIMSCALE, DIMALTF, DIMLFAC, DIMTVP and DIMTFAC as numbers, DIMFXL and DIMFXLON for DIMFXLEN and its
        /// switch, colours by index and name, BYBLOCK and BYLAYER, the fill colour 168 AutoCAD keeps of RGB 10, 20, 30, a
        /// lineweight as its number, DIMTXTDIRECTION run into its value.
        /// </summary>
        [Fact]
        public void ListOfADimensionOverridingEveryVariable_WritesEachAsAutoCADDoes()
        {
            CadDocument document = Drawing();
            string style = Handle(TextStyle(document, "GHSTYLE"));
            string dot = Handle(Arrow(document, "Dot"));
            string tick = Handle(Arrow(document, "Architectural tick"));
            string oblique = Handle(Arrow(document, "Oblique"));
            var dashed = new DxfLinetypeTableRecord { Name = "DASHED" };
            document.Database.LinetypeTable.Add(dashed);
            string handle = Add(
                document,
                Of("rotated"),
                "72:1070:0",
                "372:1070:-2",
                "371:1070:30",
                "347:1005:" + dashed.Handle,
                "346:1005:" + dashed.Handle,
                "345:1005:" + dashed.Handle,
                "344:1005:" + tick,
                "343:1005:" + dot,
                "341:1005:" + oblique,
                "340:1005:" + style,
                "294:1070:1",
                "289:1070:1",
                "288:1070:1",
                "286:1070:2",
                "285:1070:1",
                "284:1070:4",
                "283:1070:0",
                "282:1070:1",
                "281:1070:1",
                "280:1070:3",
                "279:1070:2",
                "278:1070:44",
                "277:1070:4",
                "276:1070:1",
                "275:1070:1",
                "274:1070:1",
                "273:1070:4",
                "272:1070:3",
                "271:1070:2",
                "179:1070:2",
                "178:1070:3",
                "177:1070:256",
                "176:1070:0",
                "175:1070:1",
                "174:1070:1",
                "173:1070:1",
                "172:1070:1",
                "171:1070:3",
                "170:1070:1",
                "90:1070:1",
                "79:1070:2",
                "78:1070:8",
                "77:1070:2",
                "76:1070:1",
                "74:1070:0",
                "73:1070:0",
                "71:1070:1",
                "70:1070:168",
                "69:1070:2",
                "148:1040:0.25",
                "147:1040:-1",
                "146:1040:0.75",
                "145:1040:1",
                "144:1040:0.001",
                "143:1040:2",
                "142:1040:1.5",
                "141:1040:-2.5",
                "50:1040:1.0471975511966",
                "290:1070:1",
                "49:1040:12",
                "48:1040:0.2",
                "47:1040:0.1",
                "46:1040:2",
                "45:1040:0.5",
                "44:1040:1.25",
                "43:1040:7",
                "42:1040:0.5",
                "40:1040:10",
                "4:1000:[]\"",
                "3:1000:<> mm",
                "140:1040:2.123456789"
            );

            Assert.Equal(
                new[]
                {
                    "dimension style overrides:",
                    " DIMADEC     2",
                    " DIMALT      On",
                    " DIMALTD     3",
                    " DIMALTF     2.0000",
                    " DIMALTRND   0.2500",
                    " DIMALTTD    1",
                    " DIMALTTZ    2",
                    " DIMALTU     4",
                    " DIMALTZ     1",
                    " DIMAPOST    []\"",
                    " DIMARCSYM   1",
                    " DIMATFIT    1",
                    " DIMAUNIT    1",
                    " DIMAZIN     2",
                    " DIMBLK1     Dot",
                    " DIMBLK2     ArchTick",
                    " DIMCEN      -2.5000",
                    " DIMCLRD     BYBLOCK",
                    " DIMCLRE     BYLAYER",
                    " DIMCLRT     3 (green)",
                    " DIMDEC      2",
                    " DIMDLE      2.0000",
                    " DIMDLI      7.0000",
                    " DIMDSEP     ,",
                    " DIMEXE      1.2500",
                    " DIMEXO      0.5000",
                    " DIMFRAC     1",
                    " DIMFXL      12.0000",
                    " DIMFXLON    On",
                    " DIMGAP      -1.0000",
                    " DIMJOGANG   60",
                    " DIMJUST     3",
                    " DIMLDRBLK   Oblique",
                    " DIMLFAC     0.0010",
                    " DIMLIM      Off",
                    " DIMLTEX1    DASHED",
                    " DIMLTEX2    DASHED",
                    " DIMLTYPE    DASHED",
                    " DIMLUNIT    4",
                    " DIMLWD      30",
                    " DIMLWE      -2",
                    " DIMPOST     <> mm",
                    " DIMRND      0.5000",
                    " DIMSAH      On",
                    " DIMSCALE    10.0000",
                    " DIMSD1      On",
                    " DIMSD2      On",
                    " DIMSE2      On",
                    " DIMSOXD     On",
                    " DIMTAD      2",
                    " DIMTDEC     3",
                    " DIMTFAC     0.7500",
                    " DIMTFILL    2",
                    " DIMTFILLCLR 168",
                    " DIMTIH      Off",
                    " DIMTIX      On",
                    " DIMTM       0.2000",
                    " DIMTMOVE    2",
                    " DIMTOFL     On",
                    " DIMTOH      Off",
                    " DIMTOL      On",
                    " DIMTOLJ     0",
                    " DIMTP       0.1000",
                    " DIMTSZ      1.5000",
                    " DIMTVP      1.0000",
                    " DIMTXSTY    GHSTYLE",
                    " DIMTXT      2.1235",
                    " DIMTXTDIRECTIONOn",
                    " DIMTZIN     4",
                    " DIMUPT      On",
                    " DIMZIN      8",
                },
                Overrides(document, handle)
            );
        }

        /// <summary>
        /// AutoCAD 2021 lists a user arrow by its block's name, a leader arrow of none as ClosedFilled, colours 8 and 255 as their
        /// numbers, 2 and 4 by name, the lineweights -1 and -3, an empty DIMPOST as its name alone and a point as DIMDSEP.
        /// </summary>
        [Fact]
        public void ListOfAUserArrowNoLeaderArrowAndPlainColours_WritesThemAsAutoCADDoes()
        {
            CadDocument document = Drawing();
            var mine = new DxfBlockTableRecord { Name = "MyArrow" };
            document.Database.BlockTable.Add(mine);
            mine.AppendEntity(new DxfCircle(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), 0.5));
            string handle = Add(
                document,
                Of("rotated"),
                "342:1005:" + mine.Handle,
                "341:1005:0",
                "176:1070:8",
                "177:1070:255",
                "178:1070:2",
                "70:1070:4",
                "371:1070:-1",
                "372:1070:-3",
                "3:1000:",
                "278:1070:46",
                "71:1070:0"
            );

            Assert.Equal(
                new[]
                {
                    "dimension style overrides:",
                    " DIMBLK      MyArrow",
                    " DIMCLRD     8",
                    " DIMCLRE     255",
                    " DIMCLRT     2 (yellow)",
                    " DIMDSEP     .",
                    " DIMLDRBLK   ClosedFilled",
                    " DIMLWD      -1",
                    " DIMLWE      -3",
                    " DIMPOST",
                    " DIMTFILLCLR 4 (cyan)",
                    " DIMTOL      Off",
                },
                Overrides(document, handle)
            );
        }

        /// <summary>
        /// AutoCAD 2021 writes the distances of overrides in the drawing's units, nought feet and nought inches after whole feet
        /// left out in engineering and architectural units, the scale to $LUPREC places as a plain number, and DIMJOGANG in the
        /// drawing's angle units: DIMASZ 5, DIMDLE 0.0001, DIMEXE 12, DIMEXO 0.03125, DIMGAP -1.25, DIMJOGANG 45 degrees,
        /// DIMSCALE 10, DIMTM 1 000.5, DIMTP 0 and DIMTXT 15.5 so.
        /// </summary>
        [Theory]
        [InlineData(
            1,
            4,
            2,
            2,
            "5.0000E+00|1.0000E-04|1.2000E+01|3.1250E-02|-1.2500E+00|50.00g|10.0000|1.0005E+03|0.0000E+00|1.5500E+01"
        )]
        [InlineData(3, 2, 3, 4, "5.00\"|0.00\"|1'|0.03\"|-1.25\"|0.7854r|10.00|83'-4.50\"|0.00\"|1'-3.50\"")]
        [InlineData(3, 0, 4, 0, "5\"|0\"|1'|0\"|-1\"|N 45d E|10|83'-5\"|0\"|1'-4\"")]
        [InlineData(4, 4, 4, 2, "5\"|0\"|1'|1/16\"|-1 1/4\"|N 45d0' E|10.0000|83'-4 1/2\"|0\"|1'-3 1/2\"")]
        [InlineData(4, 0, 1, 0, "5\"|0\"|1'|0\"|-1\"|45d|10|83'-5\"|0\"|1'-4\"")]
        [InlineData(5, 3, 0, 2, "5|0|12|0|-1 1/4|45.00|10.000|1000 1/2|0|15 1/2")]
        [InlineData(2, 0, 0, 8, "5|0|12|0|-1|45.00000000|10|1001|0|16")]
        public void ListOfOverridesInOtherUnits_WritesThemInTheDrawingsUnits_AsAutoCADDoes(
            int lunits,
            int luprec,
            int aunits,
            int auprec,
            string expected
        )
        {
            CadDocument document = Drawing();
            document.Database.Lunits = lunits;
            document.Database.Luprec = luprec;
            document.Database.Aunits = aunits;
            document.Database.Auprec = auprec;
            string handle = Add(
                document,
                Of("rotated"),
                "41:1040:5",
                "44:1040:12",
                "140:1040:15.5",
                "47:1040:0",
                "147:1040:-1.25",
                "40:1040:10",
                "50:1040:0.785398163397448",
                "46:1040:0.0001",
                "42:1040:0.03125",
                "48:1040:1000.5"
            );
            string[] names =
            {
                "DIMASZ",
                "DIMDLE",
                "DIMEXE",
                "DIMEXO",
                "DIMGAP",
                "DIMJOGANG",
                "DIMSCALE",
                "DIMTM",
                "DIMTP",
                "DIMTXT",
            };
            string[] values = expected.Split('|');

            List<string> lines = Overrides(document, handle);

            Assert.Equal(
                new[] { "dimension style overrides:" }.Concat(names.Select((n, i) => " " + n.PadRight(12) + values[i])),
                lines
            );
        }
    }
}
