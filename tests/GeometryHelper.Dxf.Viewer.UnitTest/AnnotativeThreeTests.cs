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
    /// Annotative dimensions of the kinds and states round 3 added, against AutoCAD 2021's (probes of the plugin GHA3 in
    /// accoreconsole, the template's style Annotative at CANNOSCALE 1:50, given 1:100 by -OBJECTSCALE): arc length and jogged
    /// radius dimensions, words moved or turned, breaks, an angular dimension's DIMSCALE override, and the attributes of an
    /// INSERT of an annotative block.
    /// </summary>
    public class AnnotativeThreeTests
    {
        private static CadDocument Scaled()
        {
            CadDocument document = Blank();
            DxfObjectContextCollection scales = document.Database.ObjectContextManager.GetContextCollection(
                "ACDB_ANNOTATIONSCALES"
            );

            foreach (double d in new[] { 1.0, 50.0, 100.0 })
            {
                scales.AddContext(
                    new DxfAnnotationScale
                    {
                        Name = "1:" + d.ToString("0"),
                        PaperUnits = 1,
                        DrawingUnits = d,
                    }
                );
            }

            var style = new DxfDimStyleTableRecord { Name = "Annotative" };
            document.Database.DimStyleTable.Add(style);
            style.Dimscale = 0.0;
            style.Annotative = DxfAnnotativeStates.True;
            document.Database.Dimstyle = "Annotative";
            document.Database.SetDimstyleData(style);
            document.Database.Cannoscale = "1:50";
            return document;
        }

        private static string[] Names(DxfDBObject o) => o.GetContexts().Select(c => c.Name).ToArray();

        private static string Last(CadDocument document) => document.ModelSpace.Block.Cast<DxfEntity>().Last().Handle;

        private static void AddScale(CadDocument document, string handle, string scale = "1:100") =>
            Run(Session(document), "-OBJECTSCALE", new[] { handle }, Enter, "A", scale, Enter);

        /// <summary>The groups of the context of an entry of an object, as "code=value" each, the numbers as the drawing's text holds them.</summary>
        private static string Context(CadDocument document, DxfDBObject o, string key)
        {
            var manager = (DxfDBDictionary)o.ExtensionDictionary.GetAt("AcDbContextDataManager");
            var scales = (DxfDBDictionary)manager.GetAt("ACDB_ANNOTATIONSCALES");
            string text = document.Database.ToText();
            int at = text.IndexOf("\n  5\n" + scales.GetAt(key).Handle + "\n", StringComparison.Ordinal);
            Assert.True(at >= 0);
            int from = text.LastIndexOf("  0\n", at, StringComparison.Ordinal);
            int to = text.IndexOf("\n  0\n", at, StringComparison.Ordinal);
            string[] lines = text.Substring(from, to - from).Split('\n');
            var groups = new List<string>();

            for (int i = 2; i + 1 < lines.Length; i += 2)
            {
                groups.Add(lines[i].Trim() + "=" + lines[i + 1].Trim());
            }

            return string.Join(" ", groups);
        }

        private static double Number(string groups, string code)
        {
            string token = groups.Split(' ').First(t => t.StartsWith(code + "=", StringComparison.Ordinal));
            return double.Parse(token.Substring(code.Length + 1), CultureInfo.InvariantCulture);
        }

        private static DxfBlockTableRecord SecondPicture(CadDocument document, DxfDimension dimension) =>
            document.Database.BlockTable.Single(b =>
                b.Name.StartsWith("*D", StringComparison.Ordinal) && b.Name != dimension.DimBlock.Name
            );

        private static DxfMText Words(DxfBlockTableRecord picture) => picture.OfType<DxfMText>().Single();

        private static List<DxfLine> LinesAt(DxfBlockTableRecord picture, double y) =>
            picture
                .OfType<DxfLine>()
                .Where(l => Math.Abs(l.StartPoint.Y - y) < 1E-6 && Math.Abs(l.EndPoint.Y - y) < 1E-6)
                .OrderBy(l => Math.Min(l.StartPoint.X, l.EndPoint.X))
                .ToList();

        [Fact]
        public void AnArcLengthDimensionDrawnByAnAnnotativeStyle_IsAnnotative_AndGivenASecondScale()
        {
            CadDocument document = Scaled();
            CadCommandSession session = Run(Session(document), "ARC", P(0, 0), P(100, 100), P(200, 0));
            string arc = Last(document);
            Run(session, "DIMARC", new[] { arc }, P(100, 160));
            var dimension = Assert.Single(All<DxfArcDimension>(document.ModelSpace));

            Assert.Equal(DxfAnnotativeStates.True, dimension.Annotative);
            Assert.Equal(new[] { "1:50" }, Names(dimension));
            Assert.Equal(9.0, Words(dimension.DimBlock).TextHeight, 6);

            AddScale(document, dimension.Handle);

            // AutoCAD 2021 (probe GHA3, a DIMARC at 1:50 and 1:100): an angular context of the arc's own point, the words twice
            // as high in a picture of their own, the dimension arc where it was.
            Assert.Equal(new[] { "1:50", "1:100" }, Names(dimension));
            Assert.Equal(18.0, Words(SecondPicture(document, dimension)).TextHeight, 6);
            string second = Context(document, dimension, "*A2");
            Assert.Contains("100=AcDbAngularDimensionObjectContextData", second, StringComparison.Ordinal);
            Assert.Equal(180.0, Number(second, "11"), 3);
            Assert.Equal(138.564, Number(second, "21"), 3);
            Assert.Equal(160.0, Number(second, "20"), 3);
        }

        [Fact]
        public void AJoggedRadiusDimensionDrawnByAnAnnotativeStyle_IsAnnotative_AndGivenASecondScale()
        {
            CadDocument document = Scaled();
            CadCommandSession session = Run(Session(document), "C", P(1000, 0), "500");
            string circle = Last(document);
            Run(session, "DIMJOGGED", new[] { circle }, P(1000, -1000), P(1800, 200), P(1400, -400));
            var dimension = Assert.Single(All<DxfRadialDimensionLarge>(document.ModelSpace));

            Assert.Equal(DxfAnnotativeStates.True, dimension.Annotative);

            AddScale(document, dimension.Handle);

            // AutoCAD 2021 (probe GHA3, a DIMJOGGED at 1:50 and 1:100): the words not at their default place (294 0), the chord
            // point, then the centre it is measured from and the jog.
            Assert.Equal(new[] { "1:50", "1:100" }, Names(dimension));
            Assert.Equal(18.0, Words(SecondPicture(document, dimension)).TextHeight, 6);
            string second = Context(document, dimension, "*A2");
            Assert.Contains("100=AcDbRadialDimensionObjectContextData", second, StringComparison.Ordinal);
            Assert.Contains("100=AcDbRadialDimensionLargeObjectContextData", second, StringComparison.Ordinal);
            Assert.Equal(0.0, Number(second, "294"), 9);
            Assert.Equal(1000.0, Number(second, "12"), 3);
            Assert.Equal(-1000.0, Number(second, "22"), 3);
        }

        [Fact]
        public void ADimensionWhoseWordsDimtEditMoved_IsGivenASecondScale_TheWordsWhereTheyWerePut_AndTheArrowsOut()
        {
            CadDocument document = Scaled();
            CadCommandSession session = Run(Session(document), "DIMLINEAR", P(0, 300), P(100, 300), P(50, 330));
            var dimension = Assert.Single(All<DxfDimension>(document.ModelSpace));
            Run(session, "DIMTEDIT", new[] { dimension.Handle }, P(70, 350));

            Assert.False(dimension.UsingDefaultTextPosition);
            string first = Context(document, dimension, "*A1");
            Assert.Equal(0.0, Number(first, "294"), 9);

            AddScale(document, dimension.Handle);

            // AutoCAD 2021 (probe GHA3, words DIMTEDIT moved to (70, 350)): at 1:100 the words stay at (70, 350) and the arrowheads,
            // 18 long, stand outside the extension lines on 18 more of line; the dimension line's point follows the record.
            DxfBlockTableRecord second = SecondPicture(document, dimension);
            Assert.Equal(70.0, Words(second).Location.X, 6);
            Assert.Equal(350.0, Words(second).Location.Y, 6);
            List<DxfLine> line = LinesAt(second, 350.0);
            Assert.Equal(2, line.Count);
            Assert.Equal(-36.0, Math.Min(line[0].StartPoint.X, line[0].EndPoint.X), 6);
            Assert.Equal(-18.0, Math.Max(line[0].StartPoint.X, line[0].EndPoint.X), 6);
            Assert.Equal(118.0, Math.Min(line[1].StartPoint.X, line[1].EndPoint.X), 6);
            Assert.Equal(136.0, Math.Max(line[1].StartPoint.X, line[1].EndPoint.X), 6);
            string context = Context(document, dimension, "*A2");
            Assert.Equal(0.0, Number(context, "294"), 9);
            Assert.Equal(70.0, Number(context, "10"), 6);
            Assert.Equal(350.0, Number(context, "20"), 6);
            Assert.Equal(350.0, Number(context, "21"), 6);
            Assert.Equal(350.0, Number(first, "21"), 6);
        }

        [Fact]
        public void ADimensionWhoseWordsWereTurned_HoldsTheirAngleInEachContext()
        {
            CadDocument document = Scaled();
            CadCommandSession session = Run(
                Session(document),
                "DIMLINEAR",
                P(0, 1200),
                P(100, 1200),
                "A",
                "20",
                P(50, 1230)
            );
            var dimension = Assert.Single(All<DxfDimension>(document.ModelSpace));

            AddScale(document, dimension.Handle);

            // AutoCAD 2021 (probe GHA3, words turned 20 degrees): 140 is 0.3490658503988659, in radians, in both contexts, the
            // words in the default place (294 1); at 1:100 they stand outside the line's end, the arrows inside.
            foreach (string key in new[] { "*A1", "*A2" })
            {
                string context = Context(document, dimension, key);
                Assert.Equal(20.0 * Math.PI / 180.0, Number(context, "140"), 9);
                Assert.Equal(1.0, Number(context, "294"), 9);
            }

            DxfBlockTableRecord second = SecondPicture(document, dimension);
            Assert.True(Words(second).Location.X > 118.0);
        }

        [Fact]
        public void ABreakMadeByHandInAnAnnotativeDimension_ShowsInEachPicture_AtTheSamePlace()
        {
            CadDocument document = Scaled();
            CadCommandSession session = Run(Session(document), "DIMLINEAR", P(200, 0), P(400, 0), P(300, 40));
            var dimension = Assert.Single(All<DxfDimension>(document.ModelSpace));
            Run(session, "DIMBREAK", new[] { dimension.Handle }, "M", P(225, 40), P(235, 40));

            AddScale(document, dimension.Handle);

            // AutoCAD 2021 (probe GHA3): the two points the break was made between hold at both scales.
            foreach (DxfBlockTableRecord picture in new[] { dimension.DimBlock, SecondPicture(document, dimension) })
            {
                List<DxfLine> line = LinesAt(picture, 40.0);
                Assert.Contains(line, l => Math.Abs(Math.Max(l.StartPoint.X, l.EndPoint.X) - 225.0) < 1E-6);
                Assert.Contains(line, l => Math.Abs(Math.Min(l.StartPoint.X, l.EndPoint.X) - 235.0) < 1E-6);
                Assert.DoesNotContain(line, l =>
                    Math.Min(l.StartPoint.X, l.EndPoint.X) < 229.0 && Math.Max(l.StartPoint.X, l.EndPoint.X) > 231.0
                );
            }
        }

        [Fact]
        public void ABreakMadeAtAnObjectInAnAnnotativeDimension_IsAsWideAsItsScaleMakesIt()
        {
            CadDocument document = Scaled();
            CadCommandSession session = Run(Session(document), "L", P(230, -50), P(230, 100), Enter);
            Run(session, "DIMLINEAR", P(200, 0), P(400, 0), P(300, 40));
            var dimension = Assert.Single(All<DxfDimension>(document.ModelSpace));
            Run(session, "DIMBREAK", new[] { dimension.Handle }, Enter);

            AddScale(document, dimension.Handle);

            // AutoCAD 2021 (probe GHA3): the break size 0.125 over the scale, 6.25 at 1:50 and 12.5 at 1:100, about the crossing.
            List<DxfLine> first = LinesAt(dimension.DimBlock, 40.0);
            Assert.Contains(first, l => Math.Abs(Math.Max(l.StartPoint.X, l.EndPoint.X) - 226.875) < 1E-6);
            Assert.Contains(first, l => Math.Abs(Math.Min(l.StartPoint.X, l.EndPoint.X) - 233.125) < 1E-6);
            List<DxfLine> second = LinesAt(SecondPicture(document, dimension), 40.0);
            Assert.Contains(second, l => Math.Abs(Math.Max(l.StartPoint.X, l.EndPoint.X) - 223.75) < 1E-6);
            Assert.Contains(second, l => Math.Abs(Math.Min(l.StartPoint.X, l.EndPoint.X) - 236.25) < 1E-6);
        }

        [Fact]
        public void AnAngularDimensionDrawnByAnAnnotativeStyle_HoldsDimscaleZeroAsAnOverride()
        {
            CadDocument document = Scaled();
            CadCommandSession session = Run(Session(document), "L", P(0, 0), P(100, 0), Enter);
            string one = Last(document);
            Run(session, "L", P(0, 0), P(70, 70), Enter);
            string two = Last(document);
            Run(session, "DIMANGULAR", new[] { one }, new[] { two }, P(60, 20));
            var dimension = Assert.Single(All<DxfLineAngularDimension2>(document.ModelSpace));

            // AutoCAD 2021 (probe GHA3): DSTYLE {40 0.0} on the angular dimension of the style Annotative; a linear one has none.
            IReadOnlyList<DxfTypedValue> data = dimension.GetXDataForApplication("ACAD");
            int at = data.ToList().FindIndex(v => v.TypeCode == 1070 && v.Value.Trim() == "40");
            Assert.True(at >= 0);
            Assert.Equal(1040, data[at + 1].TypeCode);
            Assert.Equal(0.0, double.Parse(data[at + 1].Value, CultureInfo.InvariantCulture), 9);
        }

        [Fact]
        public void AnInsertOfAnAnnotativeBlock_IsShownAtCannoscaleFromTheStart_ItsAttributeToo()
        {
            CadDocument document = Scaled();
            DxfDatabase db = document.Database;
            var block = new DxfBlockTableRecord { Name = "BA" };
            db.BlockTable.Add(block);
            block.AppendEntity(new DxfCircle(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), 20.0));
            block.AppendEntity(
                new DxfAttributeDefinition
                {
                    Tag = "TAG1",
                    Prompt = "PR",
                    TextString = "DEF",
                    Position = new GeoPoint3(25, 0, 0),
                    Height = 5.0,
                }
            );
            block.Annotative = DxfAnnotativeStates.True;

            Run(Session(document), "-INSERT", "BA", P(500, 0), "1", "1", "0", "VAL");
            var insert = Assert.Single(All<DxfBlockReference>(document.ModelSpace));
            DxfAttributeReference attribute = Assert.Single(insert.AttributeCollection);

            // AutoCAD 2021's -INSERT (probe GHA3, at 1:50): the factors 50 for 1, a context of 1:50, and one for the attribute at
            // its point (525, 0), which has 550 at 1:100, twice as far from the INSERT.
            Assert.Equal(50.0, insert.ScaleFactors.X, 9);
            Assert.Equal(new[] { "1:50" }, Names(insert));
            Assert.Equal(new[] { "1:50" }, Names(attribute));
            Assert.Equal(525.0, Number(Context(document, attribute, "*A1"), "10"), 6);

            AddScale(document, insert.Handle);

            Assert.Equal(new[] { "1:50", "1:100" }, Names(insert));
            Assert.Equal(new[] { "1:50", "1:100" }, Names(attribute));
            Assert.Equal(550.0, Number(Context(document, attribute, "*A2"), "10"), 6);
        }
    }
}
