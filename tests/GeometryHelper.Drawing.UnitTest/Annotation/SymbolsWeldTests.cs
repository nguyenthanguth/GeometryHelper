using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Drawing.Annotation;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Drawing.UnitTest.Annotation.AnnotationKit;
using static GeometryHelper.Drawing.UnitTest.Annotation.MarksKit;
using static GeometryHelper.Drawing.UnitTest.Projection.ProjectionKit;

namespace GeometryHelper.Drawing.UnitTest.Annotation
{
    public class SymbolsWeldTests
    {
        private static IReadOnlyList<WeldSymbol> Find(params DrawingObject[] objects)
            => WeldSymbol.Find(Project(objects, Front), objects, Front, new SymbolSettings(), 10);

        /// <summary>Writes a symbol at 1:10 at the origin and gives the drawing.</summary>
        private static DxfDatabase Write(WeldSymbol symbol)
        {
            var database = new DxfDatabase("AC1027");
            symbol.WriteTo(database, new ViewPlacement2("V-1", new GeoPoint2(0, 0), 10));
            return database;
        }

        [Fact]
        public void AFilletWeldOnTheArrowSideStandsOnTheReferenceLineWithItsSizeAndLength()
        {
            DrawingObject plate = Part("P-1", Box(0, 0, 0, 300, 200, 20), true);
            DrawingObject weld = Weld("W-1", new GeoPoint3(100, 0, 30), "Fillet", "Arrow", "a5", "200", "P-1", "P-2");

            WeldSymbol symbol = Assert.Single(Find(plate, weld));

            Assert.Equal("W-W-1", symbol.Id);
            Assert.Equal(WeldType.Fillet, symbol.Type);
            Assert.Equal(WeldSide.Arrow, symbol.Side);
            Assert.Equal("a5", symbol.Size);
            Assert.Equal("200", symbol.Length);
            Near(100, symbol.ArrowTip.X);
            Near(30, symbol.ArrowTip.Y);
            Near(150, symbol.Knee.X);
            Near(80, symbol.Knee.Y);
            Assert.Equal(new[] { "W-1", "P-1", "P-2" }, symbol.Links.ObjectIds);

            // Half a height, "a5" and 0.3 of a height, the triangle a height, 0.3 and "200", half a height, each character a
            // height wide: 0.5 + 2 + 0.3 + 1 + 0.3 + 3 + 0.5 = 7.6 heights of 25.
            Near(7.6 * 25, symbol.ReferenceLine.Length);

            DxfDatabase database = Write(symbol);
            DxfLine[] lines = database.ModelSpace.OfType<DxfLine>().ToArray();
            Assert.Equal(6, lines.Length);
            Assert.Single(lines, l => l.Linetype == "HIDDEN");
            Assert.Single(database.ModelSpace.OfType<DxfSolid>());
            Assert.Equal(new[] { "a5", "200" }, database.ModelSpace.OfType<DxfMText>().Select(t => t.Contents));

            // The triangle stands on the continuous line, 8 up on the sheet, and rises from it.
            Assert.All(lines.Where(l => l.Linetype != "HIDDEN" && Math.Abs(l.StartPoint.X - l.EndPoint.X) < 1E-9 && l.StartPoint.X > 15), l => Assert.True(Math.Min(l.StartPoint.Y, l.EndPoint.Y) >= 8 - 1E-9));
            Assert.All(database.ModelSpace, e => Assert.Equal(SymbolSettings.Layer, e.Layer));
        }

        [Fact]
        public void AWeldOnTheOtherSideHangsFromTheDashedLineAndOneOnBothSidesHasNoDashedLine()
        {
            DrawingObject other = Weld("W-1", new GeoPoint3(0, 0, 0), "VButt", "other", "s8", null);
            DrawingObject both = Weld("W-2", new GeoPoint3(500, 0, 0), "Fillet", "Both", "a4", null);
            IReadOnlyList<WeldSymbol> symbols = Find(other, both);

            DxfDatabase otherDrawn = Write(symbols[0]);
            DxfLine dashed = Assert.Single(otherDrawn.ModelSpace.OfType<DxfLine>(), l => l.Linetype == "HIDDEN");
            DxfLine[] vee = otherDrawn.ModelSpace.OfType<DxfLine>().Where(l => Math.Abs(l.StartPoint.Y - l.EndPoint.Y) > 1E-9 && l.StartPoint.X > symbols[0].Knee.X / 10 + 1E-9).ToArray();
            Assert.Equal(2, vee.Length);
            Assert.All(vee, l => Assert.True(Math.Max(l.StartPoint.Y, l.EndPoint.Y) <= dashed.StartPoint.Y + 1E-9));

            DxfDatabase bothDrawn = Write(symbols[1]);
            Assert.DoesNotContain(bothDrawn.ModelSpace.OfType<DxfLine>(), l => l.Linetype == "HIDDEN");
            Assert.Equal(new[] { "a4", "a4" }, bothDrawn.ModelSpace.OfType<DxfMText>().Select(t => t.Contents));
            Assert.Equal(2 + 2 * 3, bothDrawn.ModelSpace.OfType<DxfLine>().Count());
        }

        [Fact]
        public void EachKindOfWeldDrawsItsOwnBasicSymbol()
        {
            var strokes = new Dictionary<string, int>();

            foreach (string type in new[] { "Fillet", "SquareButt", "VButt", "BevelButt" })
            {
                WeldSymbol symbol = Assert.Single(Find(Weld("W-1", new GeoPoint3(0, 0, 0), type, "Arrow", null, null)));
                strokes[type] = Write(symbol).ModelSpace.OfType<DxfLine>().Count() - 3;
            }

            Assert.Equal(3, strokes["Fillet"]);
            Assert.Equal(2, strokes["SquareButt"]);
            Assert.Equal(2, strokes["VButt"]);
            Assert.Equal(2, strokes["BevelButt"]);
        }

        [Fact]
        public void AWeldOfNoKindIsAFilletAndOneOfAKindNotKnownGetsNoSymbol()
        {
            IReadOnlyList<WeldSymbol> symbols = Find(Weld("W-1", new GeoPoint3(0, 0, 0), null, null, null, null), Weld("W-2", new GeoPoint3(300, 0, 0), "Plug", null, null, null));

            WeldSymbol symbol = Assert.Single(symbols);
            Assert.Equal("W-1", symbol.WeldId);
            Assert.Equal(WeldType.Fillet, symbol.Type);
            Assert.Equal(WeldSide.Arrow, symbol.Side);
        }

        [Fact]
        public void AwsWeldSymbolsAreNotDrawnYetAndSaySo()
        {
            DrawingObject[] objects = { Weld("W-1", new GeoPoint3(0, 0, 0), "Fillet", "Arrow", "a5", null) };

            NotSupportedException error = Assert.Throws<NotSupportedException>(() => WeldSymbol.Find(Project(objects, Front), objects, Front, new SymbolSettings { WeldStandard = WeldStandard.AwsA24 }, 10));
            Assert.Contains("Q3", error.Message);
        }
    }
}
