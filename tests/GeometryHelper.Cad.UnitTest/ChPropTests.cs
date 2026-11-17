using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// CHPROP against AutoCAD 2021's console: "Select objects:" and "Enter property to change [Color/LAyer/LType/ltScale/LWeight/
    /// Thickness/TRansparency/Material/Annotative]:", the properties of CHANGE's Properties option less Elev, asked and
    /// applied by the same code, and PLotstyle added to both where the drawing has named plot styles.
    /// </summary>
    public class ChPropTests
    {
        private const string ChPropProperties =
            "Enter property to change [Color/LAyer/LType/ltScale/LWeight/Thickness/TRansparency/Material/Annotative]:";

        private const string ChangeProperties =
            "Enter property to change [Color/Elev/LAyer/LType/ltScale/LWeight/Thickness/TRansparency/Material/Annotative]:";

        private static CadDocument TwoLines(out string first, out string second)
        {
            CadDocument document = Blank();
            document.Database.LayerTable.Add(new DxfLayerTableRecord { Name = "WALLS" });
            var dashed = new DxfLinetypeTableRecord { Name = "DASHED", NumDashes = 2 };
            dashed.SetDashLengthAt(0, 12.5);
            dashed.SetDashLengthAt(1, -6.25);
            document.Database.LinetypeTable.Add(dashed);
            var a = new DxfLine { StartPoint = new GeoPoint3(0, 0, 0), EndPoint = new GeoPoint3(100, 0, 0) };
            var b = new DxfLine { StartPoint = new GeoPoint3(0, 50, 0), EndPoint = new GeoPoint3(100, 50, 0) };
            document.ApplyEdit("draw", null, s =>
            {
                s.Append(document.ModelSpace, a);
                s.Append(document.ModelSpace, b);
            });
            first = a.Handle;
            second = b.Handle;
            return document;
        }

        /// <summary>The handle of the plot style an object names (group 390), read from the text the drawing writes; null for none.</summary>
        private static string PlotStyleOf(CadDocument document, string handle)
        {
            string text = document.Database.ToText();
            int start = text.IndexOf("\n  5\n" + handle + "\n", StringComparison.Ordinal);
            string common = text.Substring(
                start,
                text.IndexOf("100\nAcDbLine\n", start, StringComparison.Ordinal) - start
            );
            int at = common.IndexOf("390\n", StringComparison.Ordinal);
            return at < 0 ? null : common.Substring(at + 4).Split('\n')[0].Trim();
        }

        [Fact]
        public void ChPropAsksAutoCadsPrompts_AndChangeAsksElevToo()
        {
            CadDocument document = TwoLines(out string first, out _);
            CadCommandSession session = Session(document);

            Run(session, "CHPROP");
            Assert.Equal("Select objects:", session.PromptText);
            Run(session, new[] { first }, Enter);
            Assert.Equal(ChPropProperties, session.PromptText);
            Run(session, Esc);

            Run(session, "CHANGE", new[] { first }, Enter, "P");
            Assert.Equal(ChangeProperties, session.PromptText);
        }

        [Fact]
        public void ChPropSetsColourLayerLinetypeScaleLineweightAndThickness_OnEveryObjectSelected()
        {
            CadDocument document = TwoLines(out string first, out string second);
            CadCommandSession session = Session(document);

            Run(session, "CHPROP", new[] { first, second }, Enter);
            Run(session, "C", "1", "LA", "WALLS", "LT", "DASHED", "LTS", "2", "LW", "0.5", "T", "7", Enter);

            foreach (string handle in new[] { first, second })
            {
                DxfLine line = Entity<DxfLine>(document, handle);
                Assert.Equal(1, line.Color.ColorIndex);
                Assert.Equal("WALLS", line.Layer);
                Assert.Equal("DASHED", line.Linetype);
                Assert.Equal(2.0, line.LinetypeScale);
                Assert.Equal(DxfLineWeight.LineWeight050, line.LineWeight);
                Assert.Equal(7.0, line.Thickness);
            }

            Assert.Equal("CHPROP", document.NextUndo.Name);
        }

        [Fact]
        public void ChPropAndChangePropertiesGiveTheSameResult()
        {
            CadDocument byChprop = TwoLines(out string a, out _);
            CadDocument byChange = TwoLines(out string b, out _);

            Run(Session(byChprop), "CHPROP", new[] { a }, Enter, "C", "3", "LA", "WALLS", "TR", "40", Enter);
            Run(Session(byChange), "CHANGE", new[] { b }, Enter, "P", "C", "3", "LA", "WALLS", "TR", "40", Enter);

            DxfLine x = Entity<DxfLine>(byChprop, a);
            DxfLine y = Entity<DxfLine>(byChange, b);
            Assert.Equal(y.Color.ColorIndex, x.Color.ColorIndex);
            Assert.Equal(y.Layer, x.Layer);
            Assert.Equal(y.Transparency, x.Transparency);
            Assert.True(x.Transparency.IsByAlpha);
        }

        [Fact]
        public void AnUnknownLayer_IsSaidSo_AndAskedAgain()
        {
            CadDocument document = TwoLines(out string first, out _);
            CadCommandSession session = Session(document);

            Run(session, "CHPROP", new[] { first }, Enter, "LA", "NOPE");

            Assert.Contains("Cannot find layer \"NOPE\".", session.History);
            Assert.Equal("Enter new layer name <0>:", session.PromptText);
        }

        [Fact]
        public void PLotstyle_IsOfferedOnlyWhereTheDrawingHasNamedPlotStyles_AndSetsAndClearsTheStyle()
        {
            var db = new DxfDatabase("AC1027");
            db.SetHeader("$PSTYLEMODE", new DxfTypedValue(290, "0"));
            var line = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0));
            db.ModelSpace.AppendEntity(line);
            string name = db.PlotStyleNameDictionary.First().Key;
            CadDocument document = CadDocument.From(db, "plot.dxf");
            CadCommandSession session = Session(document);

            Run(session, "CHPROP", new[] { line.Handle }, Enter);
            Assert.Equal(
                "Enter property to change [Color/LAyer/LType/ltScale/LWeight/Thickness/TRansparency/Material/PLotstyle/Annotative]:",
                session.PromptText
            );

            Run(session, "PL", name, Enter);
            Assert.Equal(db.PlotStyleNameDictionary.GetAt(name).Handle, PlotStyleOf(document, line.Handle));

            Run(session, "CHPROP", new[] { line.Handle }, Enter, "PL", "ByLayer", Enter);
            Assert.Null(PlotStyleOf(document, line.Handle));
        }

        [Fact]
        public void PLotstyleIsNotOffered_WhereThePlotStylesFollowTheColour()
        {
            CadDocument document = TwoLines(out string first, out _);
            CadCommandSession session = Session(document);

            Run(session, "CHPROP", new[] { first }, Enter);

            Assert.DoesNotContain("PLotstyle", session.PromptText);
        }
    }
}
