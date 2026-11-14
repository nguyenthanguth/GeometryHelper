using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// FILTER's Transparency, MultiLine Style, Shape Position and Shape Name lines, and the kinds of object that stay as names of
    /// AutoCAD 2021's Select Filter list (Material, Position Marker, the surfaces made by blending, networking, patching,
    /// filleting, offsetting and extending), with the list in the order of acfilterres.dll. The drawing: lines T1 to T5 with
    /// the transparency ByLayer, ByBlock, 50, 70 and 0 per cent, an MLINE M1 in the style WALLS and M2 in STANDARD, a SHAPE S1
    /// named BOX at (5, 6) and S2 named ARROW at (50, 60).
    /// </summary>
    public class SelectionFilterTransparencyShapeTests
    {
        private const int ByBlock = 0x01000000;

        // The alpha of 128 is 50 per cent clear, 76 70 per cent, 255 solid.
        private static int Alpha(int alpha) => 0x02000000 | alpha;

        private static string Segment(string handle, params object[] extra) =>
            TestDrawings.Entity(
                "LINE",
                "0",
                256,
                handle,
                new object[] { "10", 0.0, "20", 0.0, "30", 0.0, "11", 10.0, "21", 0.0, "31", 0.0 }
                    .Concat(extra)
                    .ToArray()
            );

        private static CadDocument Drawing() =>
            CadDocument.From(
                TestDrawings.Drawing(
                    null,
                    null,
                    Segment("T1"),
                    Segment("T2", "440", ByBlock),
                    Segment("T3", "440", Alpha(128)),
                    Segment("T4", "440", Alpha(76)),
                    Segment("T5", "440", Alpha(255)),
                    TestDrawings.Entity("MLINE", "0", 256, "M1", "2", "WALLS", "10", 0.0, "20", 0.0, "30", 0.0),
                    TestDrawings.Entity("MLINE", "0", 256, "M2", "2", "STANDARD", "10", 0.0, "20", 0.0, "30", 0.0),
                    TestDrawings.Entity(
                        "SHAPE",
                        "0",
                        256,
                        "S1",
                        "2",
                        "BOX",
                        "10",
                        5.0,
                        "20",
                        6.0,
                        "30",
                        0.0,
                        "40",
                        2.0
                    ),
                    TestDrawings.Entity(
                        "SHAPE",
                        "0",
                        256,
                        "S2",
                        "2",
                        "ARROW",
                        "10",
                        50.0,
                        "20",
                        60.0,
                        "30",
                        0.0,
                        "40",
                        2.0
                    )
                ),
                "tr.dxf"
            );

        private static string[] Pass(CadSelectionFilter filter) =>
            filter.Apply(Drawing(), new[] { "T1", "T2", "T3", "T4", "T5", "M1", "M2", "S1", "S2" }).ToArray();

        private static CadSelectionFilter Of(params CadFilterLine[] lines)
        {
            var filter = new CadSelectionFilter();

            foreach (CadFilterLine line in lines)
            {
                filter.Lines.Add(line);
            }

            return filter;
        }

        private static CadFilterLine Line(CadFilterProperty property, string value, params string[] operators) =>
            new CadFilterLine(property, value, operators);

        [Fact]
        public void Choices_HaveTheItemsOfTheResourceFile_InItsOrder()
        {
            string[] names = CadSelectionFilter.Choices.Select(c => c.Name).ToArray();

            Assert.Equal("MultiLine Style", names[Array.IndexOf(names, "MultiLine") + 1]);
            Assert.Equal("Shape Position", names[Array.IndexOf(names, "Shape") + 1]);
            Assert.Equal("Shape Name", names[Array.IndexOf(names, "Shape") + 2]);
            Assert.Equal("Material", names[Array.IndexOf(names, "Light") + 1]);
            Assert.Equal("Surface(Blend)", names[Array.IndexOf(names, "Surface(Nurbs)") + 1]);
            Assert.Equal(
                new[]
                {
                    "Section Plane",
                    "Surface(Network)",
                    "Surface(Patch)",
                    "Surface(Fillet)",
                    "Surface(Offset)",
                    "Surface(Extend)",
                    "Transparency",
                    "Position Marker",
                    "Thickness",
                },
                names.Skip(Array.IndexOf(names, "Section Plane")).Take(9)
            );
            Assert.Null(CadSelectionFilter.Choices.First(c => c.Name == "Transparency").Value);
            Assert.Equal("Position Marker", CadSelectionFilter.Choices.First(c => c.Name == "Position Marker").Value);
        }

        [Fact]
        public void Transparency_IsTestedByPerCent_ByLayerAndByBlockBelowAll()
        {
            Assert.Equal(new[] { "T3" }, Lines("50"));
            Assert.Equal(new[] { "T1" }, Lines("ByLayer"));
            Assert.Equal(new[] { "T2" }, Lines("By Block"));
            Assert.Equal(new[] { "T4" }, Lines("60", ">="));
            Assert.Equal(new[] { "T3", "T4" }, Lines("50", ">="));
            Assert.Equal(new[] { "T5" }, Lines("0"));
            Assert.Equal(new[] { "T1", "T2", "T3", "T4", "T5" }, Lines("90", "<="));
            Assert.Equal(new[] { "T1", "T2", "T4", "T5" }, Lines("50", "!="));
        }

        // The lines T1 to T5 that pass the Object = Line line and a Transparency line.
        private static string[] Lines(string value, params string[] operators) =>
            Pass(Of(Line(CadFilterProperty.Object, "Line"), Line(CadFilterProperty.Transparency, value, operators)));

        [Fact]
        public void Transparency_OfAnObjectThatHasNone_PassesOnlyByLayer()
        {
            // An MLINE has no transparency of its own: it draws by its layer, as ByLayer.
            Assert.Contains("M1", Pass(Of(Line(CadFilterProperty.Transparency, "ByLayer"))));
        }

        [Theory]
        [InlineData("abc")]
        [InlineData("-5")]
        [InlineData("91")]
        [InlineData("")]
        public void Transparency_AValueThatIsNoPerCent_IsRefused(string value)
        {
            ArgumentException refused = Assert.Throws<ArgumentException>(() =>
                Line(CadFilterProperty.Transparency, value)
            );

            Assert.StartsWith("Invalid Value.", refused.Message);
        }

        [Fact]
        public void TransparencyLine_ShowsItsValue_AsTheListShowsIt()
        {
            Assert.Equal("Transparency         = 50", Line(CadFilterProperty.Transparency, "50").Text);
            Assert.Equal("Transparency         >= ByLayer", Line(CadFilterProperty.Transparency, "bylayer", ">=").Text);
            Assert.Equal("ByBlock", Line(CadFilterProperty.Transparency, "By Block").Value);
        }

        [Fact]
        public void MultilineStyle_IsAPatternOnTheNameOfTheStyle()
        {
            Assert.Equal(new[] { "M1" }, Pass(Of(Line(CadFilterProperty.MultilineStyle, "WALL*"))));
            Assert.Equal(new[] { "M2" }, Pass(Of(Line(CadFilterProperty.MultilineStyle, "standard"))));
            Assert.Equal(new[] { "M1", "M2" }, Pass(Of(Line(CadFilterProperty.Object, "MultiLine"))));
        }

        [Fact]
        public void ShapeName_AndPosition_AreReadFromTheShape()
        {
            Assert.Equal(new[] { "S1", "S2" }, Pass(Of(Line(CadFilterProperty.Object, "Shape"))));
            Assert.Equal(new[] { "S2" }, Pass(Of(Line(CadFilterProperty.ShapeName, "AR*"))));
            Assert.Equal(new[] { "S1" }, Pass(Of(Line(CadFilterProperty.ShapePosition, "10,10,0", "<", "<", "*"))));
            Assert.Equal(new[] { "S2" }, Pass(Of(Line(CadFilterProperty.ShapePosition, "10,10,0", ">=", "*", "*"))));
        }

        [Fact]
        public void AddSelectedObject_OfAShapeOrAnMline_AddsItsNameAndPosition()
        {
            CadDocument document = Drawing();
            var shape = CadSelectionFilter.LinesOf(document.FindEntity("S1"));
            var mline = CadSelectionFilter.LinesOf(document.FindEntity("M1"));

            Assert.Equal(
                new[] { "Object               = Shape", "Shape Name           = BOX" },
                shape
                    .Where(l => l.Property == CadFilterProperty.Object || l.Property == CadFilterProperty.ShapeName)
                    .Select(l => l.Text)
            );
            Assert.Contains(shape, l =>
                l.Property == CadFilterProperty.ShapePosition && l.Value == "5.0000,6.0000,0.0000"
            );
            Assert.Contains(mline, l => l.Property == CadFilterProperty.MultilineStyle && l.Value == "WALLS");
        }

        [Fact]
        public void Nfl_WritesTheNewLines_AsAutoCADsCodesAndReadsThemBack()
        {
            CadSelectionFilter filter = Of(
                Line(CadFilterProperty.Object, "Shape"),
                Line(CadFilterProperty.ShapeName, "BOX"),
                Line(CadFilterProperty.ShapePosition, "5,6,0", ">=", "*", "*"),
                Line(CadFilterProperty.MultilineStyle, "WALLS"),
                Line(CadFilterProperty.Transparency, "50", ">="),
                Line(CadFilterProperty.Transparency, "ByBlock"),
                Line(CadFilterProperty.Transparency, "ByLayer"),
                Line(CadFilterProperty.Object, "Surface(Patch)"),
                Line(CadFilterProperty.Object, "Position Marker")
            );
            Assert.Null(filter.SaveAs("New"));

            string[] rows = filter.ToNfl().Split('\n');

            Assert.Equal("(0 . \"SHAPE\")", rows[2]);
            Assert.Equal("(2 . \"BOX\")", rows[3]);
            Assert.Equal("(-4 . \">=,*,*\")", rows[4]);
            Assert.Equal("(10 5.0 6.0 0.0)", rows[5]);
            Assert.Equal("(2 . \"WALLS\")", rows[6]);
            Assert.Equal("(-4 . \">=\")", rows[7]);
            Assert.Equal("(440 . " + Alpha(128).ToString() + ")", rows[8]);
            Assert.Equal("(440 . " + ByBlock + ")", rows[9]);
            Assert.Equal("(440 . 0)", rows[10]);

            var again = new CadSelectionFilter();
            Assert.Null(again.LoadNfl(filter.ToNfl()));
            Assert.True(again.Restore("New"));
            Assert.Equal(filter.Lines.Select(l => l.Text), again.Lines.Select(l => l.Text));
            Assert.Equal(filter.ToNfl(), again.ToNfl());
        }
    }
}
