using System;
using System.Linq;
using GeometryHelper.Drawing.Annotation;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Drawing.UnitTest.Annotation.AnnotationKit;
using static GeometryHelper.Drawing.UnitTest.Projection.ProjectionKit;

namespace GeometryHelper.Drawing.UnitTest.Annotation
{
    public class SymbolsMarkTests
    {
        private static readonly ViewPlacement2 Placement = new ViewPlacement2("V-1", new GeoPoint2(0, 0), 20);

        [Fact]
        public void ASectionMarkLooksSquareToItsCutLineTheWayItIsAsked()
        {
            var mark = new SectionMark("S-A", "A", new GeoLine2(1500, -200, 1500, 200), new GeoVector2(1, 0.3), 50);

            Near(1, mark.Direction.X);
            Near(0, mark.Direction.Y);

            // The letters' boxes start 20 past the tips of arrows 100 long, each 50 wide: their middles 145 off the cut line.
            Near(1645, mark.LetterBoxes[0].Center.X);
            Near(-200, mark.LetterBoxes[0].Center.Y);
            Near(200, mark.LetterBoxes[1].Center.Y);
        }

        [Fact]
        public void ASectionMarkIsWrittenAsItsChainLineThickEndsArrowsAndLetters()
        {
            var mark = new SectionMark("S-A", "A", new GeoLine2(1500, -200, 1500, 200), new GeoVector2(-1, 0), 50);
            var database = new DxfDatabase("AC1027");

            mark.WriteTo(database, Placement);

            DxfEntity[] all = database.ModelSpace.ToArray();
            Assert.Equal(5, all.OfType<DxfLine>().Count());
            Assert.Equal(2, all.OfType<DxfSolid>().Count());
            Assert.Equal(new[] { "A", "A" }, all.OfType<DxfMText>().Select(t => t.Contents));
            Assert.Single(all.OfType<DxfLine>(), l => l.Linetype == "CENTER");
            Assert.Equal(2, all.OfType<DxfLine>().Count(l => l.LineWeight == DxfLineWeight.LineWeight050));
            Assert.All(all, e => Assert.Equal(SymbolSettings.Layer, e.Layer));
            Assert.All(all, e => Assert.True(DrawingEntityTags.TryRead(e, out string id, out _) && id == "S-A"));
            Near(2.5, all.OfType<DxfMText>().First().TextHeight);
        }

        [Fact]
        public void ADetailMarkDrawsItsCircleOrRectangleAndItsLetterAtTheUpperRight()
        {
            DetailMark circle = DetailMark.Circle("D-B", "B", new GeoPoint2(0, 0), 250, 50);
            DetailMark rectangle = DetailMark.Rectangle("D-C", "C", new GeoRectangle2(new GeoPoint2(100, 50), 200, 100), 50);

            Near(191.78, Math.Round(circle.LetterBox.Center.X - 25, 2));
            Near(240, rectangle.LetterBox.Center.X);
            Near(140, rectangle.LetterBox.Center.Y);

            var database = new DxfDatabase("AC1027");
            circle.WriteTo(database, Placement);
            rectangle.WriteTo(database, Placement);

            DxfCircle drawn = Assert.Single(database.ModelSpace.OfType<DxfCircle>());
            Near(12.5, drawn.Radius);
            DxfPolyline outline = Assert.Single(database.ModelSpace.OfType<DxfPolyline>());
            Assert.True(outline.Closed);
            Assert.Equal(4, outline.NumberOfVertices);
            Assert.Equal(new[] { "B", "C" }, database.ModelSpace.OfType<DxfMText>().Select(t => t.Contents));
        }

        [Fact]
        public void ALevelMarkWritesItsElevationInMillimetresWithItsSign()
        {
            Assert.Equal("+25", new LevelMark("L-1", new GeoPoint2(0, 25), 25, 125).Text);
            Assert.Equal("±0", new LevelMark("L-2", new GeoPoint2(0, 0), 0.2, 125).Text);
            Assert.Equal("-150", new LevelMark("L-3", new GeoPoint2(0, -150), -150, 125).Text);

            var level = new LevelMark("L-1", new GeoPoint2(0, 25), 3250, 125);
            var database = new DxfDatabase("AC1027");
            level.WriteTo(database, new ViewPlacement2("V-1", new GeoPoint2(0, 0), 50));

            DxfPolyline triangle = Assert.Single(database.ModelSpace.OfType<DxfPolyline>());
            Assert.Equal(3, triangle.NumberOfVertices);
            Near(0, triangle.GetPoint2dAt(0).X);
            Near(0.5, triangle.GetPoint2dAt(0).Y);
            Assert.Single(database.ModelSpace.OfType<DxfLine>());
            Assert.Equal("+3250", Assert.Single(database.ModelSpace.OfType<DxfMText>()).Contents);
        }

        [Fact]
        public void BadArgumentsAreRefused()
        {
            Assert.Throws<ArgumentException>(() => new SectionMark("S", "A", new GeoLine2(0, 0, 0, 0), new GeoVector2(1, 0), 1));
            Assert.Throws<ArgumentException>(() => new SectionMark("S", "A", new GeoLine2(0, 0, 100, 0), new GeoVector2(1, 0), 1));
            Assert.Throws<ArgumentException>(() => new SectionMark("S", "", new GeoLine2(0, 0, 100, 0), new GeoVector2(0, 1), 1));
            Assert.Throws<ArgumentNullException>(() => new SectionMark(null, "A", new GeoLine2(0, 0, 100, 0), new GeoVector2(0, 1), 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SectionMark("S", "A", new GeoLine2(0, 0, 100, 0), new GeoVector2(0, 1), 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => DetailMark.Circle("D", "B", new GeoPoint2(0, 0), 0, 1));
            Assert.Throws<ArgumentException>(() => DetailMark.Rectangle("D", "B", new GeoRectangle2(new GeoPoint2(0, 0), 0, 10), 1));
            Assert.Throws<ArgumentException>(() => new LevelMark("L", new GeoPoint2(0, 0), double.NaN, 1));
            Assert.Throws<ArgumentNullException>(() => new SectionMark("S", "A", new GeoLine2(0, 0, 100, 0), new GeoVector2(0, 1), 1).WriteTo(null, Placement));
        }
    }
}
